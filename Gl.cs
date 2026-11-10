using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// Every P/Invoke below is source-generated (LibraryImport); nothing is marshalled at run time.
[assembly: DisableRuntimeMarshalling]

namespace Makarov.NeuralNet3D;

// The OpenGL 1.1 (fixed-function) calls the viewer uses, straight from opengl32.dll.
internal static partial class Gl
{
    private const string Library = "opengl32.dll";

    public const uint GL_POINTS = 0x0000;
    public const uint GL_LINES = 0x0001;
    public const uint GL_TRIANGLES = 0x0004;
    public const uint GL_DEPTH_BUFFER_BIT = 0x0100;
    public const uint GL_COLOR_BUFFER_BIT = 0x4000;
    public const uint GL_LEQUAL = 0x0203;
    public const uint GL_SMOOTH = 0x1D01;
    public const uint GL_MODELVIEW = 0x1700;
    public const uint GL_PROJECTION = 0x1701;
    public const uint GL_DEPTH_TEST = 0x0B71;
    public const uint GL_LIGHTING = 0x0B50;
    public const uint GL_LIGHT0 = 0x4000;
    public const uint GL_POSITION = 0x1203;
    public const uint GL_AMBIENT = 0x1200;
    public const uint GL_DIFFUSE = 0x1201;
    public const uint GL_EMISSION = 0x1600;
    public const uint GL_COLOR_MATERIAL = 0x0B57;
    public const uint GL_FRONT_AND_BACK = 0x0408;
    public const uint GL_AMBIENT_AND_DIFFUSE = 0x1602;
    public const uint GL_LIGHT_MODEL_AMBIENT = 0x0B53;
    public const uint GL_BLEND = 0x0BE2;
    public const uint GL_SRC_ALPHA = 0x0302;
    public const uint GL_ONE = 0x0001;
    public const uint GL_NORMALIZE = 0x0BA1;
    public const uint GL_LINE_SMOOTH = 0x0B20;
    public const uint GL_POINT_SMOOTH = 0x0B10;
    public const uint GL_LINE_SMOOTH_HINT = 0x0C52;
    public const uint GL_POINT_SMOOTH_HINT = 0x0C51;
    public const uint GL_NICEST = 0x1102;
    public const uint GL_COMPILE = 0x1300;

    [LibraryImport(Library)] public static partial void glClear(uint mask);
    [LibraryImport(Library)] public static partial void glClearColor(float red, float green, float blue, float alpha);
    [LibraryImport(Library)] public static partial void glEnable(uint capability);
    [LibraryImport(Library)] public static partial void glDisable(uint capability);
    [LibraryImport(Library)] public static partial void glBegin(uint mode);
    [LibraryImport(Library)] public static partial void glEnd();
    [LibraryImport(Library)] public static partial void glVertex3f(float x, float y, float z);
    [LibraryImport(Library)] public static partial void glNormal3f(float x, float y, float z);
    [LibraryImport(Library)] public static partial void glColor4f(float red, float green, float blue, float alpha);
    [LibraryImport(Library)] public static partial void glMatrixMode(uint mode);
    [LibraryImport(Library)] public static partial void glLoadIdentity();
    [LibraryImport(Library)] public static partial void glPushMatrix();
    [LibraryImport(Library)] public static partial void glPopMatrix();
    [LibraryImport(Library)] public static partial void glTranslatef(float x, float y, float z);
    [LibraryImport(Library)] public static partial void glRotatef(float angle, float x, float y, float z);
    [LibraryImport(Library)] public static partial void glScalef(float x, float y, float z);
    [LibraryImport(Library)] public static partial void glViewport(int x, int y, int width, int height);
    [LibraryImport(Library)] public static partial void glFrustum(double left, double right, double bottom, double top, double near, double far);
    [LibraryImport(Library)] public static partial void glDepthFunc(uint function);
    [LibraryImport(Library)] public static partial void glDepthMask([MarshalAs(UnmanagedType.U1)] bool write);
    [LibraryImport(Library)] public static partial void glShadeModel(uint mode);
    [LibraryImport(Library)] public static partial void glBlendFunc(uint source, uint destination);
    [LibraryImport(Library)] public static partial void glLineWidth(float width);
    [LibraryImport(Library)] public static partial void glPointSize(float size);
    [LibraryImport(Library)] public static partial void glHint(uint target, uint mode);
    [LibraryImport(Library)] public static partial void glLightfv(uint light, uint parameter, ReadOnlySpan<float> values);
    [LibraryImport(Library)] public static partial void glLightModelfv(uint parameter, ReadOnlySpan<float> values);
    [LibraryImport(Library)] public static partial void glMaterialfv(uint face, uint parameter, ReadOnlySpan<float> values);
    [LibraryImport(Library)] public static partial void glColorMaterial(uint face, uint mode);
    [LibraryImport(Library)] public static partial uint glGenLists(int range);
    [LibraryImport(Library)] public static partial void glNewList(uint list, uint mode);
    [LibraryImport(Library)] public static partial void glEndList();
    [LibraryImport(Library)] public static partial void glCallList(uint list);
}

// Window-system side: a pixel format and a WGL context on a window's device context.
internal static partial class Wgl
{
    public const uint PFD_DOUBLEBUFFER = 0x0001;
    public const uint PFD_DRAW_TO_WINDOW = 0x0004;
    public const uint PFD_SUPPORT_OPENGL = 0x0020;
    public const byte PFD_TYPE_RGBA = 0;
    public const byte PFD_MAIN_PLANE = 0;

    [LibraryImport("opengl32.dll")] public static partial nint wglCreateContext(nint dc);

    [LibraryImport("opengl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool wglMakeCurrent(nint dc, nint context);

    [LibraryImport("opengl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool wglDeleteContext(nint context);

    [LibraryImport("gdi32.dll")] public static partial int ChoosePixelFormat(nint dc, in PIXELFORMATDESCRIPTOR descriptor);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetPixelFormat(nint dc, int format, in PIXELFORMATDESCRIPTOR descriptor);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SwapBuffers(nint dc);

    [LibraryImport("user32.dll")] public static partial nint GetDC(nint window);
    [LibraryImport("user32.dll")] public static partial int ReleaseDC(nint window, nint dc);
}

[StructLayout(LayoutKind.Sequential)]
internal struct PIXELFORMATDESCRIPTOR
{
    public ushort nSize;
    public ushort nVersion;
    public uint dwFlags;
    public byte iPixelType;
    public byte cColorBits;
    public byte cRedBits, cRedShift, cGreenBits, cGreenShift, cBlueBits, cBlueShift, cAlphaBits, cAlphaShift;
    public byte cAccumBits, cAccumRedBits, cAccumGreenBits, cAccumBlueBits, cAccumAlphaBits;
    public byte cDepthBits, cStencilBits, cAuxBuffers;
    public byte iLayerType, bReserved;
    public uint dwLayerMask, dwVisibleMask, dwDamageMask;
}
