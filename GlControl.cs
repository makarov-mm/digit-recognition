using System.Runtime.CompilerServices;

namespace Makarov.NeuralNet3D;

// A control with its own OpenGL context. The context is a legacy WGL one, so the
// fixed-function pipeline is available. RenderFrame is raised on every paint with the
// context current; if no context could be created, the control shows why instead.
internal sealed class GlControl : Control
{
    private const int CS_OWNDC = 0x0020;  // OpenGL wants a private device context

    private nint _dc;
    private nint _context;
    private string _error = "";

    public GlControl()
    {
        SetStyle(ControlStyles.Opaque | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
        DoubleBuffered = false;
    }

    public event Action? ContextCreated;

    // Client width and height in pixels.
    public event Action<int, int>? RenderFrame;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;
            parameters.ClassStyle |= CS_OWNDC;
            return parameters;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (CreateContext())
            ContextCreated?.Invoke();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        DestroyContext();
        base.OnHandleDestroyed(e);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // OpenGL covers every pixel.
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (_context == 0)
        {
            e.Graphics.Clear(BackColor);
            TextRenderer.DrawText(e.Graphics, _error, Font, ClientRectangle, ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            return;
        }
        Wgl.wglMakeCurrent(_dc, _context);
        RenderFrame?.Invoke(ClientSize.Width, ClientSize.Height);
        Wgl.SwapBuffers(_dc);
    }

    private bool CreateContext()
    {
        _dc = Wgl.GetDC(Handle);
        if (_dc == 0) return Fail("Could not get a device context for OpenGL.");

        var descriptor = new PIXELFORMATDESCRIPTOR
        {
            nSize = (ushort)Unsafe.SizeOf<PIXELFORMATDESCRIPTOR>(),
            nVersion = 1,
            dwFlags = Wgl.PFD_DRAW_TO_WINDOW | Wgl.PFD_SUPPORT_OPENGL | Wgl.PFD_DOUBLEBUFFER,
            iPixelType = Wgl.PFD_TYPE_RGBA,
            cColorBits = 32,
            cDepthBits = 24,
            iLayerType = Wgl.PFD_MAIN_PLANE,
        };
        int format = Wgl.ChoosePixelFormat(_dc, descriptor);
        if (format == 0 || !Wgl.SetPixelFormat(_dc, format, descriptor))
            return Fail("No suitable OpenGL pixel format.");

        _context = Wgl.wglCreateContext(_dc);
        if (_context == 0 || !Wgl.wglMakeCurrent(_dc, _context))
            return Fail("Could not create an OpenGL context. Is a GPU driver installed?");
        return true;
    }

    private bool Fail(string error)
    {
        DestroyContext();
        _error = error;
        return false;
    }

    private void DestroyContext()
    {
        if (_context != 0)
        {
            Wgl.wglMakeCurrent(0, 0);
            Wgl.wglDeleteContext(_context);
            _context = 0;
        }
        if (_dc != 0)
        {
            _ = Wgl.ReleaseDC(Handle, _dc);
            _dc = 0;
        }
    }
}
