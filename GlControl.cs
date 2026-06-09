using System.Runtime.InteropServices;

namespace Makarov.NeuralNet3D;

internal sealed class GlControl : Control
{
    private const int CS_OWNDC = 0x0020;

    private IntPtr _hdc;
    private IntPtr _hrc;
    private bool _ready;

    public event Action GlInit;
    public event Action<int, int> GlRender;

    public GlControl()
    {
        SetStyle(ControlStyles.Opaque | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
        DoubleBuffered = false;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ClassStyle |= CS_OWNDC;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        InitContext();
    }

    private void InitContext()
    {
        _hdc = Gl.GetDC(Handle);

        var pfd = new PIXELFORMATDESCRIPTOR
        {
            nSize = (ushort)Marshal.SizeOf<PIXELFORMATDESCRIPTOR>(),
            nVersion = 1,
            dwFlags = Gl.PFD_DRAW_TO_WINDOW | Gl.PFD_SUPPORT_OPENGL | Gl.PFD_DOUBLEBUFFER,
            iPixelType = 0,
            cColorBits = 32,
            cDepthBits = 24,
            iLayerType = 0
        };

        int pf = Gl.ChoosePixelFormat(_hdc, ref pfd);
        Gl.SetPixelFormat(_hdc, pf, ref pfd);

        _hrc = Gl.wglCreateContext(_hdc);
        Gl.wglMakeCurrent(_hdc, _hrc);

        _ready = true;
        GlInit?.Invoke();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (!_ready) return;
        Gl.wglMakeCurrent(_hdc, _hrc);
        GlRender?.Invoke(ClientSize.Width, ClientSize.Height);
        Gl.SwapBuffers(_hdc);
    }

    protected override void OnPaintBackground(PaintEventArgs pevent) { }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        _ready = false;

        if (_hrc != IntPtr.Zero)
        {
            Gl.wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
            Gl.wglDeleteContext(_hrc);
            _hrc = IntPtr.Zero;
        }

        if (_hdc != IntPtr.Zero)
        {
            Gl.ReleaseDC(Handle, _hdc);
            _hdc = IntPtr.Zero;
        }

        base.OnHandleDestroyed(e);
    }
}
