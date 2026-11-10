using System.Numerics;

namespace Makarov.NeuralNet3D;

// Draws the network in 3D with fixed-function OpenGL 1.1: neurons as lit spheres, the
// input digit as glowing pixels, the strongest connections as additive lines, a wave of
// activation running through the layers after each recognition, and 7-segment labels
// at the outputs.
internal sealed class NetworkRenderer
{
    private const float WaveSpeed = 1.7f;        // layers per second
    private const float AutoRotateSpeed = 8f;    // degrees per second
    private const int MaxInputConnections = 60;  // input pixels drawn with their connections
    private const int MaxPulses = 400;

    private static readonly Vector3 Gold = new(1.0f, 0.84f, 0.2f);
    private static readonly Vector3 PositiveColor = new(0.30f, 0.70f, 1.0f);
    private static readonly Vector3 NegativeColor = new(1.0f, 0.45f, 0.55f);
    private static readonly Vector3 LabelColor = new(0.55f, 0.85f, 1.0f);
    private static readonly float[] LightPosition = [6f, 10f, 14f, 0f];  // directional, fixed to the camera
    private static readonly float[] LightDiffuse = [0.9f, 0.9f, 0.9f, 1f];
    private static readonly float[] LightAmbient = [0.18f, 0.18f, 0.22f, 1f];
    private static readonly float[] SceneAmbient = [0.12f, 0.12f, 0.15f, 1f];

    // 7-segment glyphs: segments a..g as lines in a unit box (x right, y up), and per digit
    // a mask with bit 6 = a ... bit 0 = g.
    private static readonly (float X0, float Y0, float X1, float Y1)[] SegmentLines =
    [
        (0, 1, 1, 1), (1, 1, 1, 0.5f), (1, 0.5f, 1, 0), (0, 0, 1, 0), (0, 0.5f, 0, 0), (0, 1, 0, 0.5f), (0, 0.5f, 1, 0.5f),
    ];
    private static readonly byte[] DigitSegments =
        [0b1111110, 0b0110000, 0b1101101, 0b1111001, 0b0110011, 0b1011011, 0b1011111, 0b1110000, 0b1111111, 0b1111011];

    private readonly NeuralNetwork _network;
    private readonly Vector3[][] _positions;
    private readonly int[][] _allNeurons;  // 0..n-1 per layer
    private readonly float[] _radius;
    private readonly Vector3[] _color;
    private readonly float[][] _levels;    // what each neuron shows, 0..1, from the last result
    private readonly float[] _emission = new float[4];
    private uint _sphereList;
    private int[] _activeInputs = [];
    private int _winner = -1;
    private bool _hasResult;
    private float _wave;
    private bool _waveRunning;

    public NetworkRenderer(NeuralNetwork network)
    {
        _network = network;
        int layers = network.LayerCount;
        _positions = new Vector3[layers][];
        _allNeurons = new int[layers][];
        _levels = new float[layers][];
        _radius = new float[layers];
        _color = new Vector3[layers];
        BuildLayout();
    }

    public float Yaw { get; private set; } = -25f;
    public float Pitch { get; private set; } = -12f;
    public float Distance { get; private set; } = 30f;
    public bool AutoRotate { get; set; } = true;

    public void Rotate(float yawDegrees, float pitchDegrees)
    {
        Yaw = (Yaw + yawDegrees) % 360f;
        Pitch = Math.Clamp(Pitch + pitchDegrees, -89f, 89f);
    }

    public void Zoom(float delta) => Distance = Math.Clamp(Distance - delta, 12f, 70f);

    // Shows the network's current activations (after Forward) with `winner` highlighted,
    // and sends a wave through the layers.
    public void ShowResult(int winner)
    {
        int outputLayer = _levels.Length - 1;
        for (int l = 0; l < _levels.Length; l++)
        {
            ReadOnlySpan<float> activations = _network.Activations(l);
            if (l == 0 || l == outputLayer)
            {
                activations.CopyTo(_levels[l]);  // pixels and probabilities are already in [0, 1]
                continue;
            }
            float max = 1e-6f;
            foreach (float a in activations)
                max = MathF.Max(max, a);
            for (int i = 0; i < activations.Length; i++)
                _levels[l][i] = activations[i] / max;
        }

        _activeInputs =
        [
            .. Enumerable.Range(0, _levels[0].Length)
                .Where(i => _levels[0][i] > 0.15f)
                .OrderByDescending(i => _levels[0][i])
                .Take(MaxInputConnections),
        ];
        _winner = winner;
        _hasResult = true;
        _wave = 0f;
        _waveRunning = true;
    }

    public void ClearResult()
    {
        foreach (float[] levels in _levels)
            Array.Clear(levels);
        _activeInputs = [];
        _winner = -1;
        _hasResult = false;
        _wave = 0f;
        _waveRunning = false;
    }

    public void Update(double seconds)
    {
        float dt = (float)seconds;
        if (_waveRunning)
        {
            _wave += dt * WaveSpeed;
            float end = _positions.Length - 1;
            if (_wave >= end + 0.05f)
            {
                _wave = end;
                _waveRunning = false;
            }
        }
        if (AutoRotate)
            Yaw = (Yaw + dt * AutoRotateSpeed) % 360f;
    }

    // Needs the GL context current; call again for a new context.
    public void InitGl()
    {
        Gl.glClearColor(0.04f, 0.05f, 0.09f, 1f);
        Gl.glEnable(Gl.GL_DEPTH_TEST);
        Gl.glDepthFunc(Gl.GL_LEQUAL);
        Gl.glShadeModel(Gl.GL_SMOOTH);
        Gl.glEnable(Gl.GL_NORMALIZE);  // spheres are scaled
        Gl.glEnable(Gl.GL_LINE_SMOOTH);
        Gl.glEnable(Gl.GL_POINT_SMOOTH);
        Gl.glHint(Gl.GL_LINE_SMOOTH_HINT, Gl.GL_NICEST);
        Gl.glHint(Gl.GL_POINT_SMOOTH_HINT, Gl.GL_NICEST);
        Gl.glEnable(Gl.GL_LIGHTING);
        Gl.glEnable(Gl.GL_LIGHT0);
        Gl.glMatrixMode(Gl.GL_MODELVIEW);
        Gl.glLoadIdentity();
        Gl.glLightfv(Gl.GL_LIGHT0, Gl.GL_POSITION, LightPosition);
        Gl.glLightfv(Gl.GL_LIGHT0, Gl.GL_DIFFUSE, LightDiffuse);
        Gl.glLightfv(Gl.GL_LIGHT0, Gl.GL_AMBIENT, LightAmbient);
        Gl.glLightModelfv(Gl.GL_LIGHT_MODEL_AMBIENT, SceneAmbient);
        Gl.glEnable(Gl.GL_COLOR_MATERIAL);
        Gl.glColorMaterial(Gl.GL_FRONT_AND_BACK, Gl.GL_AMBIENT_AND_DIFFUSE);
        _sphereList = BuildSphereList();
    }

    // pixelScale: device pixels per 96-DPI pixel, for line widths and point sizes.
    public void Render(int width, int height, float pixelScale)
    {
        if (_sphereList == 0 || width <= 0 || height <= 0) return;

        Gl.glViewport(0, 0, width, height);
        Gl.glMatrixMode(Gl.GL_PROJECTION);
        Gl.glLoadIdentity();
        const double fieldOfView = 45.0 * Math.PI / 180.0, near = 0.5, far = 400.0;
        double top = near * Math.Tan(fieldOfView / 2.0);
        double right = top * width / height;
        Gl.glFrustum(-right, right, -top, top, near, far);

        Gl.glMatrixMode(Gl.GL_MODELVIEW);
        Gl.glLoadIdentity();
        Gl.glTranslatef(0f, 0f, -Distance);
        Gl.glRotatef(Pitch, 1f, 0f, 0f);
        Gl.glRotatef(Yaw, 0f, 1f, 0f);
        Gl.glClear(Gl.GL_COLOR_BUFFER_BIT | Gl.GL_DEPTH_BUFFER_BIT);

        DrawNeurons();
        if (_hasResult)
        {
            DrawConnections(pixelScale);
            DrawInputDigit();  // over the lines, so the digit stays readable
        }
        DrawLabels(pixelScale);
    }

    private void BuildLayout()
    {
        int layers = _positions.Length;
        for (int l = 0; l < layers; l++)
        {
            int count = _network.LayerSize(l);
            int columns;
            float spacing;
            if (l == 0)
            {
                columns = Dataset.Side;  // the input as a 28×28 image
                spacing = 0.30f;
                _radius[l] = 0.055f;
                _color[l] = new Vector3(0.85f, 0.95f, 1.0f);
            }
            else if (l == layers - 1)
            {
                columns = 1;
                spacing = 1.15f;
                _radius[l] = 0.30f;
                _color[l] = new Vector3(0.55f, 1.0f, 0.55f);
            }
            else
            {
                columns = (int)Math.Ceiling(Math.Sqrt(count));
                spacing = 0.95f;
                _radius[l] = 0.17f;
                _color[l] = l == 1 ? new Vector3(0.25f, 0.9f, 0.85f) : new Vector3(0.45f, 0.65f, 1.0f);
            }

            float x = -9f + 18f * l / (layers - 1);  // layers spread along x
            int rows = (count + columns - 1) / columns;
            _positions[l] = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                int row = i / columns, column = i % columns;
                _positions[l][i] = new Vector3(x, ((rows - 1) / 2f - row) * spacing, (column - (columns - 1) / 2f) * spacing);
            }
            _allNeurons[l] = [.. Enumerable.Range(0, count)];
            _levels[l] = new float[count];
        }
    }

    private static uint BuildSphereList()
    {
        const int stacks = 12, slices = 14;
        uint list = Gl.glGenLists(1);
        Gl.glNewList(list, Gl.GL_COMPILE);
        Gl.glBegin(Gl.GL_TRIANGLES);
        for (int i = 0; i < stacks; i++)
        {
            double lat0 = Math.PI * (-0.5 + (double)i / stacks);
            double lat1 = Math.PI * (-0.5 + (double)(i + 1) / stacks);
            for (int j = 0; j < slices; j++)
            {
                double lng0 = 2 * Math.PI * j / slices;
                double lng1 = 2 * Math.PI * (j + 1) / slices;
                SphereVertex(lng0, lat0); SphereVertex(lng1, lat0); SphereVertex(lng1, lat1);
                SphereVertex(lng0, lat0); SphereVertex(lng1, lat1); SphereVertex(lng0, lat1);
            }
        }
        Gl.glEnd();
        Gl.glEndList();
        return list;

        static void SphereVertex(double longitude, double latitude)
        {
            var p = new Vector3(
                (float)(Math.Cos(longitude) * Math.Cos(latitude)),
                (float)(Math.Sin(longitude) * Math.Cos(latitude)),
                (float)Math.Sin(latitude));
            Gl.glNormal3f(p.X, p.Y, p.Z);  // unit sphere: the normal is the position
            Gl.glVertex3f(p.X, p.Y, p.Z);
        }
    }

    private void DrawNeurons()
    {
        Gl.glEnable(Gl.GL_LIGHTING);
        Gl.glDepthMask(true);
        int outputLayer = _positions.Length - 1;
        for (int l = 0; l < _positions.Length; l++)
        {
            float gate = NeuronGate(l);
            for (int i = 0; i < _positions[l].Length; i++)
            {
                float level = Math.Clamp(_levels[l][i], 0f, 1f) * gate;
                bool winner = l == outputLayer && i == _winner;
                Vector3 color = winner ? Gold : _color[l] * (0.25f + 0.75f * level);
                Gl.glColor4f(color.X, color.Y, color.Z, 1f);
                SetEmission(color * (winner ? 0.9f : level));
                DrawSphere(_positions[l][i], _radius[l] * (1f + 0.35f * level) * (winner ? 1.6f : 1f));
            }
        }
        SetEmission(Vector3.Zero);
    }

    private void DrawInputDigit()
    {
        float gate = NeuronGate(0);
        if (gate <= 0.01f) return;

        Gl.glDisable(Gl.GL_LIGHTING);
        for (int i = 0; i < _positions[0].Length; i++)
        {
            float v = Math.Clamp(_levels[0][i], 0f, 1f);
            if (v < 0.12f) continue;
            float brightness = (0.35f + 0.65f * v) * gate;
            Gl.glColor4f(brightness, 0.97f * brightness, 0.82f * brightness, 1f);
            DrawSphere(_positions[0][i], _radius[0] * (2.2f + 1.8f * v));
        }
        Gl.glEnable(Gl.GL_LIGHTING);
    }

    private void DrawConnections(float pixelScale)
    {
        BeginAdditive();
        Gl.glLineWidth(1.1f * pixelScale);
        Gl.glBegin(Gl.GL_LINES);
        for (int l = 0; l < _positions.Length - 1; l++)
        {
            float gate = ConnectionGate(l);
            if (gate < 0.01f) continue;
            float strength = (l == 0 ? 1.0f : 1.6f) * gate;
            int[] sources = Sources(l);
            for (int j = 0; j < _positions[l + 1].Length; j++)
                foreach (int i in sources)
                {
                    float weight = _network.Weight(l, i, j);
                    float alpha = MathF.Abs(weight) * _levels[l][i] * strength;
                    if (alpha < 0.03f) continue;
                    Vector3 color = weight >= 0f ? PositiveColor : NegativeColor;
                    Gl.glColor4f(color.X, color.Y, color.Z, MathF.Min(alpha, 0.8f));
                    Vertex(_positions[l][i]);
                    Vertex(_positions[l + 1][j]);
                }
        }
        Gl.glEnd();
        DrawPulses(pixelScale);
        EndAdditive();
    }

    // Sparks travelling along the strongest connections of the layer gap the wave is in.
    private void DrawPulses(float pixelScale)
    {
        if (!_waveRunning) return;
        int gap = (int)MathF.Floor(_wave);
        if (gap < 0 || gap >= _positions.Length - 1) return;

        float t = _wave - gap;
        float travel = SmoothStep(0f, 1f, t);
        float fade = 1f - MathF.Abs(2f * t - 1f);
        int[] sources = Sources(gap);

        Gl.glPointSize(7f * pixelScale);
        Gl.glColor4f(1.0f, 1.0f, 0.75f, MathF.Min(1f, 0.7f * fade + 0.3f));
        Gl.glBegin(Gl.GL_POINTS);
        int drawn = 0;
        for (int j = 0; j < _positions[gap + 1].Length && drawn < MaxPulses; j++)
            foreach (int i in sources)
            {
                if (MathF.Abs(_network.Weight(gap, i, j)) * _levels[gap][i] < 0.25f) continue;
                Vertex(Vector3.Lerp(_positions[gap][i], _positions[gap + 1][j], travel));
                if (++drawn == MaxPulses) break;
            }
        Gl.glEnd();
    }

    private void DrawLabels(float pixelScale)
    {
        BeginAdditive();
        // Glyphs lie in the camera's plane, so they stay readable from any angle, and sit
        // on the outer side of the output layer as seen from the camera.
        (Vector3 right, Vector3 up) = CameraAxes();
        bool outerSideIsRight = right.X >= 0f;
        Vector3[] outputs = _positions[^1];
        float[] probabilities = _levels[^1];
        for (int digit = 0; digit < Math.Min(outputs.Length, DigitSegments.Length); digit++)
        {
            bool winner = digit == _winner;
            float scale = winner ? 0.95f : 0.62f;
            float brightness = 0.45f + 0.55f * Math.Clamp(probabilities[digit], 0f, 1f);
            Vector3 color = winner ? Gold : LabelColor * brightness;
            float width = 0.55f * scale, height = 0.9f * scale;
            Vector3 offset = outerSideIsRight ? right * 0.75f : -right * (0.75f + width);
            Vector3 origin = outputs[digit] + offset - up * (height / 2f);

            Gl.glLineWidth((winner ? 3f : 2f) * pixelScale);
            Gl.glColor4f(color.X, color.Y, color.Z, 0.95f);
            Gl.glBegin(Gl.GL_LINES);
            for (int s = 0; s < SegmentLines.Length; s++)
            {
                if ((DigitSegments[digit] >> (SegmentLines.Length - 1 - s) & 1) == 0) continue;
                (float x0, float y0, float x1, float y1) = SegmentLines[s];
                Vertex(origin + right * (x0 * width) + up * (y0 * height));
                Vertex(origin + right * (x1 * width) + up * (y1 * height));
            }
            Gl.glEnd();
        }
        EndAdditive();
    }

    // Camera right and up axes in world space, from the rotations used in Render.
    private (Vector3 Right, Vector3 Up) CameraAxes()
    {
        float yaw = Yaw * MathF.PI / 180f, pitch = Pitch * MathF.PI / 180f;
        var right = new Vector3(MathF.Cos(yaw), 0f, MathF.Sin(yaw));
        var up = new Vector3(MathF.Sin(pitch) * MathF.Sin(yaw), MathF.Cos(pitch), -MathF.Sin(pitch) * MathF.Cos(yaw));
        return (right, up);
    }

    // Input pixels: only the brightest ones, or the picture drowns in lines.
    private int[] Sources(int layer) => layer == 0 ? _activeInputs : _allNeurons[layer];

    private float NeuronGate(int layer) => SmoothStep(layer - 0.5f, layer + 0.1f, _wave);

    private float ConnectionGate(int gap) => SmoothStep(gap, gap + 0.8f, _wave);

    private static float SmoothStep(float edge0, float edge1, float x)
    {
        float t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static void BeginAdditive()
    {
        Gl.glDisable(Gl.GL_LIGHTING);
        Gl.glEnable(Gl.GL_BLEND);
        Gl.glBlendFunc(Gl.GL_SRC_ALPHA, Gl.GL_ONE);  // overlapping lines add up to a glow
        Gl.glDepthMask(false);
    }

    private static void EndAdditive()
    {
        Gl.glDepthMask(true);
        Gl.glDisable(Gl.GL_BLEND);
        Gl.glEnable(Gl.GL_LIGHTING);
    }

    private void DrawSphere(Vector3 centre, float radius)
    {
        Gl.glPushMatrix();
        Gl.glTranslatef(centre.X, centre.Y, centre.Z);
        Gl.glScalef(radius, radius, radius);
        Gl.glCallList(_sphereList);
        Gl.glPopMatrix();
    }

    private void SetEmission(Vector3 color)
    {
        _emission[0] = color.X;
        _emission[1] = color.Y;
        _emission[2] = color.Z;
        _emission[3] = 1f;
        Gl.glMaterialfv(Gl.GL_FRONT_AND_BACK, Gl.GL_EMISSION, _emission);
    }

    private static void Vertex(Vector3 p) => Gl.glVertex3f(p.X, p.Y, p.Z);
}
