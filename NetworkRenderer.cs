using System.Numerics;

namespace NeuralNet3D;

internal sealed class NetworkRenderer
{
    private readonly NeuralNetwork _net;
    private readonly Vector3[][] _pos;
    private readonly float[] _radius;
    private readonly float[][] _color;
    private readonly double[][] _norm;
    private uint _sphereList;
    private bool _glReady;
    private readonly float[] _emis = new float[4];
    private readonly float[] _zero4 = { 0, 0, 0, 1 };
    public float Yaw = -25f, Pitch = -12f, Distance = 30f;
    public bool Dragging;
    public bool AutoRotate = true;
    private int _winner = -1;
    private int[] _activePix = Array.Empty<int>();
    private float _wave;
    private bool _waveRunning;
    private const float WaveSpeed = 1.7f;

    public bool Ready { get; set; }

    private static readonly string[] SegOn =
    [
        "1111110", "0110000", "1101101", "1111001", "0110011",
        "1011011", "1011111", "1110000", "1111111", "1111011"
    ];

    public NetworkRenderer(NeuralNetwork net)
    {
        _net = net;
        int L = net.Sizes.Length;
        _pos = new Vector3[L][];
        _norm = new double[L][];
        _radius = new float[L];
        _color = new float[L][];
        BuildLayout();
    }

    private void BuildLayout()
    {
        int L = _net.Sizes.Length;
        float[] layerX = new float[L];

        for (int l = 0; l < L; l++) 
            layerX[l] = -9f + 18f * (L == 1 ? 0 : (float)l / (L - 1));

        for (int l = 0; l < L; l++)
        {
            int n = _net.Sizes[l];
            _norm[l] = new double[n];

            int cols; float sp;
            if (l == 0) { cols = 28; sp = 0.30f; _radius[l] = 0.055f; _color[l] = new[] { 0.85f, 0.95f, 1.0f }; }
            else if (l == L - 1) { cols = 1; sp = 1.15f; _radius[l] = 0.30f; _color[l] = new[] { 0.55f, 1.0f, 0.55f }; }
            else
            {
                cols = (int)Math.Ceiling(Math.Sqrt(n));
                sp = 0.95f; _radius[l] = 0.17f;
                _color[l] = l == 1 ? [0.25f, 0.9f, 0.85f] : [0.45f, 0.65f, 1.0f];
            }

            int rows = (n + cols - 1) / cols;
            _pos[l] = new Vector3[n];
            for (int idx = 0; idx < n; idx++)
            {
                int r = idx / cols, c = idx % cols;
                float y = ((rows - 1) / 2f - r) * sp;
                float z = (c - (cols - 1) / 2f) * sp;
                _pos[l][idx] = new Vector3(layerX[l], y, z);
            }
        }
    }

    public void InitGl()
    {
        Gl.glClearColor(0.04f, 0.05f, 0.09f, 1f);
        Gl.glEnable(Gl.GL_DEPTH_TEST);
        Gl.glDepthFunc(Gl.GL_LEQUAL);
        Gl.glShadeModel(Gl.GL_SMOOTH);
        Gl.glEnable(Gl.GL_NORMALIZE);
        Gl.glEnable(Gl.GL_LINE_SMOOTH);
        Gl.glEnable(Gl.GL_POINT_SMOOTH);
        Gl.glHint(Gl.GL_LINE_SMOOTH_HINT, Gl.GL_NICEST);
        Gl.glHint(Gl.GL_POINT_SMOOTH_HINT, Gl.GL_NICEST);

        // освещение
        Gl.glEnable(Gl.GL_LIGHTING);
        Gl.glEnable(Gl.GL_LIGHT0);
        Gl.glLightfv(Gl.GL_LIGHT0, Gl.GL_POSITION, new[] { 6f, 10f, 14f, 0f });
        Gl.glLightfv(Gl.GL_LIGHT0, Gl.GL_DIFFUSE, new[] { 0.9f, 0.9f, 0.9f, 1f });
        Gl.glLightfv(Gl.GL_LIGHT0, Gl.GL_AMBIENT, new[] { 0.18f, 0.18f, 0.22f, 1f });
        Gl.glLightModelfv(Gl.GL_LIGHT_MODEL_AMBIENT, new[] { 0.12f, 0.12f, 0.15f, 1f });
        Gl.glEnable(Gl.GL_COLOR_MATERIAL);
        Gl.glColorMaterial(Gl.GL_FRONT_AND_BACK, Gl.GL_AMBIENT_AND_DIFFUSE);

        BuildSphereList();
        _glReady = true;
    }

    private void BuildSphereList()
    {
        _sphereList = Gl.glGenLists(1);
        Gl.glNewList(_sphereList, Gl.GL_COMPILE);
        const int stacks = 12, slices = 14;
        Gl.glBegin(Gl.GL_TRIANGLES);
        for (int i = 0; i < stacks; i++)
        {
            double lat0 = Math.PI * (-0.5 + (double)i / stacks);
            double lat1 = Math.PI * (-0.5 + (double)(i + 1) / stacks);
            double z0 = Math.Sin(lat0), zr0 = Math.Cos(lat0);
            double z1 = Math.Sin(lat1), zr1 = Math.Cos(lat1);
            for (int j = 0; j < slices; j++)
            {
                double lng0 = 2 * Math.PI * ((double)j / slices);
                double lng1 = 2 * Math.PI * ((double)(j + 1) / slices);
                V(lng0, z0, zr0); V(lng1, z0, zr0); V(lng1, z1, zr1);
                V(lng0, z0, zr0); V(lng1, z1, zr1); V(lng0, z1, zr1);
            }
        }
        Gl.glEnd();
        Gl.glEndList();
    }

    private static void V(double lng, double z, double zr)
    {
        float x = (float)(Math.Cos(lng) * zr);
        float y = (float)(Math.Sin(lng) * zr);
        float zz = (float)z;
        Gl.glNormal3f(x, y, zz);
        Gl.glVertex3f(x, y, zz);
    }

    public void SetResult(double[] probs, int winner)
    {
        _winner = winner;
        double[] inp = _net.A[0];
        var idx = new System.Collections.Generic.List<(int i, double v)>();
        for (int i = 0; i < inp.Length; i++) if (inp[i] > 0.15) idx.Add((i, inp[i]));
        idx.Sort((a, b) => b.v.CompareTo(a.v));
        int take = Math.Min(60, idx.Count);
        _activePix = new int[take];
        for (int k = 0; k < take; k++) _activePix[k] = idx[k].i;
    }

    public void StartWave() { _wave = 0f; _waveRunning = true; }

    public void Update(double dt)
    {
        if (_waveRunning)
        {
            _wave += (float)dt * WaveSpeed;
            float maxW = _net.Sizes.Length - 1;
            if (_wave >= maxW + 0.05f) { _wave = maxW; _waveRunning = false; }
        }
        if (AutoRotate && !Dragging) Yaw += (float)dt * 8f;
    }

    public void Render(int w, int h)
    {
        if (!_glReady || w <= 0 || h <= 0) return;

        Gl.glViewport(0, 0, w, h);
        Gl.glMatrixMode(Gl.GL_PROJECTION);
        Gl.glLoadIdentity();
        double aspect = w / (double)h;
        const double fov = 45.0 * Math.PI / 180.0, near = 0.5, far = 400.0;
        double top = near * Math.Tan(fov / 2.0), right = top * aspect;
        Gl.glFrustum(-right, right, -top, top, near, far);

        Gl.glMatrixMode(Gl.GL_MODELVIEW);
        Gl.glLoadIdentity();
        Gl.glTranslatef(0, 0, -Distance);
        Gl.glRotatef(Pitch, 1, 0, 0);
        Gl.glRotatef(Yaw, 0, 1, 0);

        Gl.glClear(Gl.GL_COLOR_BUFFER_BIT | Gl.GL_DEPTH_BUFFER_BIT);

        ComputeNorm();
        DrawNodes();
        if (Ready) DrawEdges();
        if (Ready) DrawInputDigit();
        DrawOutputLabels();

        Gl.glFlush();
    }

    private void ComputeNorm()
    {
        int L = _net.Sizes.Length;

        if (!Ready)
        {
            for (int l = 0; l < L; l++)
                Array.Clear(_norm[l], 0, _norm[l].Length);
            return;
        }
        for (int l = 0; l < L; l++)
        {
            double[] a = _net.A[l], n = _norm[l];
            if (l == 0 || l == L - 1)
            {
                for (int i = 0; i < a.Length; i++) n[i] = a[i];
            }
            else
            {
                double max = 1e-6;
                for (int i = 0; i < a.Length; i++) if (a[i] > max) max = a[i];
                for (int i = 0; i < a.Length; i++) n[i] = a[i] / max;
            }
        }
    }

    private static float SmoothStep(float a, float b, float x)
    {
        float t = Math.Clamp((x - a) / (b - a), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private float NodeGate(int layer) => SmoothStep(layer - 0.5f, layer + 0.1f, _wave);
    private float EdgeGate(int gap) => SmoothStep(gap, gap + 0.8f, _wave);

    private void DrawNodes()
    {
        Gl.glEnable(Gl.GL_LIGHTING);
        Gl.glDepthMask(1);

        int L = _net.Sizes.Length;
        for (int l = 0; l < L; l++)
        {
            Vector3[] p = _pos[l];
            double[] act = _norm[l];
            float[] baseCol = _color[l];
            float gate = NodeGate(l);

            for (int i = 0; i < p.Length; i++)
            {
                float a = (float)Math.Clamp(act[i], 0, 1) * gate;
                bool win = l == L - 1 && i == _winner;

                float cr, cg, cb;
                if (win)
                {
                    cr = 1.0f; 
                    cg = 0.84f; 
                    cb = 0.2f;
                }
                else
                {
                    cr = baseCol[0] * (0.25f + 0.75f * a);
                    cg = baseCol[1] * (0.25f + 0.75f * a);
                    cb = baseCol[2] * (0.25f + 0.75f * a);
                }

                Gl.glColor4f(cr, cg, cb, 1f);
                float glow = win ? 0.9f : a;
                _emis[0] = cr * glow; _emis[1] = cg * glow; _emis[2] = cb * glow; _emis[3] = 1f;
                Gl.glMaterialfv(Gl.GL_FRONT_AND_BACK, Gl.GL_EMISSION, _emis);

                float rad = _radius[l] * (1f + 0.35f * a) * (win ? 1.6f : 1f);

                Gl.glPushMatrix();
                Gl.glTranslatef(p[i].X, p[i].Y, p[i].Z);
                Gl.glScalef(rad, rad, rad);
                Gl.glCallList(_sphereList);
                Gl.glPopMatrix();
            }
        }
        Gl.glMaterialfv(Gl.GL_FRONT_AND_BACK, Gl.GL_EMISSION, _zero4);
    }

    private void DrawEdges()
    {
        Gl.glDisable(Gl.GL_LIGHTING);
        Gl.glEnable(Gl.GL_BLEND);
        Gl.glBlendFunc(Gl.GL_SRC_ALPHA, Gl.GL_ONE);
        Gl.glDepthMask(0);
        Gl.glLineWidth(1.1f);

        int L = _net.Sizes.Length;
        const float kFull = 1.6f, kInput = 1.0f;

        Gl.glBegin(Gl.GL_LINES);

        if (_activePix.Length > 0)
        {
            float eg = EdgeGate(0);
            if (eg > 0.01f)
            {
                Vector3[] src = _pos[0], dst = _pos[1];
                double[] aSrc = _norm[0];
                double[][] w = _net.W[0];
                for (int j = 0; j < dst.Length; j++)
                {
                    double[] wj = w[j];
                    Vector3 dp = dst[j];
                    for (int t = 0; t < _activePix.Length; t++)
                    {
                        int i = _activePix[t];
                        float alpha = (float)(Math.Abs(wj[i]) * aSrc[i]) * kInput * eg;
                        if (alpha < 0.03f) continue;
                        EmitEdge(src[i], dp, wj[i], alpha);
                    }
                }
            }
        }

        for (int g = 1; g < L - 1; g++)
        {
            float eg = EdgeGate(g);
            if (eg < 0.01f) continue;
            Vector3[] src = _pos[g], dst = _pos[g + 1];
            double[] aSrc = _norm[g];
            double[][] w = _net.W[g];
            for (int j = 0; j < dst.Length; j++)
            {
                double[] wj = w[j];
                Vector3 dp = dst[j];
                for (int i = 0; i < src.Length; i++)
                {
                    float alpha = (float)(Math.Abs(wj[i]) * aSrc[i]) * kFull * eg;
                    if (alpha < 0.03f) continue;
                    EmitEdge(src[i], dp, wj[i], alpha);
                }
            }
        }
        Gl.glEnd();

        DrawPulses();

        Gl.glDepthMask(1);
        Gl.glDisable(Gl.GL_BLEND);
        Gl.glEnable(Gl.GL_LIGHTING);
    }

    private static void EmitEdge(Vector3 s, Vector3 d, double weight, float alpha)
    {
        if (weight >= 0) 
            Gl.glColor4f(0.30f, 0.70f, 1.0f, Math.Min(alpha, 0.8f));
        else
            Gl.glColor4f(1.0f, 0.45f, 0.55f, Math.Min(alpha, 0.8f));

        Gl.glVertex3f(s.X, s.Y, s.Z);
        Gl.glVertex3f(d.X, d.Y, d.Z);
    }

    private void DrawPulses()
    {
        if (!_waveRunning) return;
        int gap = (int)Math.Floor(_wave);
        int L = _net.Sizes.Length;
        if (gap < 0 || gap >= L - 1) return;

        float t = _wave - gap;
        float te = SmoothStep(0f, 1f, t);
        float fade = 1f - Math.Abs(2f * t - 1f);

        Vector3[] src = _pos[gap], dst = _pos[gap + 1];
        double[] aSrc = _norm[gap];
        double[][] w = _net.W[gap];

        Gl.glPointSize(7f);
        Gl.glBegin(Gl.GL_POINTS);

        int drawn = 0;
        for (int j = 0; j < dst.Length && drawn < 400; j++)
        {
            double[] wj = w[j];
            Vector3 dp = dst[j];

            if (gap == 0)
            {
                for (int k = 0; k < _activePix.Length && drawn < 400; k++)
                {
                    int i = _activePix[k];
                    double a = Math.Abs(wj[i]) * aSrc[i];
                    if (a < 0.25) continue;
                    EmitPulse(src[i], dp, te, fade);
                    drawn++;
                }
            }
            else
            {
                for (int i = 0; i < src.Length && drawn < 400; i++)
                {
                    double a = Math.Abs(wj[i]) * aSrc[i];
                    if (a < 0.25) continue;
                    EmitPulse(src[i], dp, te, fade);
                    drawn++;
                }
            }
        }

        Gl.glEnd();
    }

    private static void EmitPulse(Vector3 s, Vector3 d, float te, float fade)
    {
        Vector3 p = s + (d - s) * te;
        Gl.glColor4f(1.0f, 1.0f, 0.75f, Math.Min(1f, 0.7f * fade + 0.3f));
        Gl.glVertex3f(p.X, p.Y, p.Z);
    }

    private void DrawInputDigit()
    {
        float gate = NodeGate(0);
        if (gate <= 0.01f) return;

        Gl.glDisable(Gl.GL_LIGHTING);
        Gl.glDisable(Gl.GL_BLEND);
        Gl.glDepthMask(1);

        Vector3[] p = _pos[0];
        double[] a = _norm[0];
        for (int i = 0; i < p.Length; i++)
        {
            float v = (float)Math.Clamp(a[i], 0, 1);
            if (v < 0.12f) continue;
            float b = (0.35f + 0.65f * v) * gate;
            Gl.glColor4f(1.0f * b, 0.97f * b, 0.82f * b, 1f);
            float rad = _radius[0] * (2.2f + 1.8f * v);
            Gl.glPushMatrix();
            Gl.glTranslatef(p[i].X, p[i].Y, p[i].Z);
            Gl.glScalef(rad, rad, rad);
            Gl.glCallList(_sphereList);
            Gl.glPopMatrix();
        }
        Gl.glEnable(Gl.GL_LIGHTING);
    }

    private void DrawOutputLabels()
    {
        int L = _net.Sizes.Length;
        Vector3[] outPos = _pos[L - 1];
        double[] probs = _net.A[L - 1];

        Gl.glDisable(Gl.GL_LIGHTING);
        Gl.glEnable(Gl.GL_BLEND);
        Gl.glBlendFunc(Gl.GL_SRC_ALPHA, Gl.GL_ONE);
        Gl.glDepthMask(0);

        int count = Math.Min(outPos.Length, 10);
        for (int d = 0; d < count; d++)
        {
            bool win = d == _winner;
            float prob = Ready ? (float)Math.Clamp(probs[d], 0, 1) : 0f;
            float scale = win ? 0.95f : 0.62f;
            float bright = win ? 1f : 0.45f + 0.55f * prob;

            float gh = 0.9f * scale;
            float ox = outPos[d].X + 0.75f;
            float oy = outPos[d].Y - gh / 2f;
            float oz = outPos[d].Z;

            float r, g, b;
            if (win) { r = 1f; g = 0.84f; b = 0.2f; }
            else { r = 0.55f * bright; g = 0.85f * bright; b = 1.0f * bright; }

            Gl.glLineWidth(win ? 3.0f : 2.0f);
            DrawGlyph(d, ox, oy, oz, scale, r, g, b);
        }

        Gl.glDepthMask(1);
        Gl.glDisable(Gl.GL_BLEND);
        Gl.glEnable(Gl.GL_LIGHTING);
    }

    private static void DrawGlyph(int digit, float ox, float oy, float oz, float scale, float r, float g, float b)
    {
        float w = 0.55f * scale, h = 0.9f * scale, m = h / 2f;
        string s = SegOn[digit];

        Gl.glColor4f(r, g, b, 0.95f);
        Gl.glBegin(Gl.GL_LINES);

        void Seg(float x0, float y0, float x1, float y1)
        {
            Gl.glVertex3f(ox + x0, oy + y0, oz);
            Gl.glVertex3f(ox + x1, oy + y1, oz);
        }

        if (s[0] == '1') Seg(0, h, w, h);
        if (s[1] == '1') Seg(w, h, w, m);
        if (s[2] == '1') Seg(w, m, w, 0);
        if (s[3] == '1') Seg(0, 0, w, 0);
        if (s[4] == '1') Seg(0, m, 0, 0);
        if (s[5] == '1') Seg(0, h, 0, m);
        if (s[6] == '1') Seg(0, m, w, m);

        Gl.glEnd();
    }
}
