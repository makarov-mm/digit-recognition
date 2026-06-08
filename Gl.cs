using System.Runtime.InteropServices;

namespace NeuralNet3D;

internal static class Gl
{
    private const string OPENGL = "opengl32.dll";
    private const string GDI = "gdi32.dll";
    private const string USER = "user32.dll";

    public const uint GL_POINTS = 0x0000;
    public const uint GL_LINES = 0x0001;
    public const uint GL_TRIANGLES = 0x0004;
    public const uint GL_DEPTH_BUFFER_BIT = 0x00000100;
    public const uint GL_COLOR_BUFFER_BIT = 0x00004000;
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
    public const uint GL_LIGHT_MODEL_AMBIENT = 0x0B53;
    public const uint PFD_DRAW_TO_WINDOW = 0x00000004;
    public const uint PFD_SUPPORT_OPENGL = 0x00000020;
    public const uint PFD_DOUBLEBUFFER = 0x00000001;

    [DllImport(OPENGL)] public static extern void glClear(uint mask);
    [DllImport(OPENGL)] public static extern void glClearColor(float r, float g, float b, float a);
    [DllImport(OPENGL)] public static extern void glEnable(uint cap);
    [DllImport(OPENGL)] public static extern void glDisable(uint cap);
    [DllImport(OPENGL)] public static extern void glBegin(uint mode);
    [DllImport(OPENGL)] public static extern void glEnd();
    [DllImport(OPENGL)] public static extern void glVertex3f(float x, float y, float z);
    [DllImport(OPENGL)] public static extern void glColor3f(float r, float g, float b);
    [DllImport(OPENGL)] public static extern void glColor4f(float r, float g, float b, float a);
    [DllImport(OPENGL)] public static extern void glNormal3f(float x, float y, float z);
    [DllImport(OPENGL)] public static extern void glMatrixMode(uint mode);
    [DllImport(OPENGL)] public static extern void glLoadIdentity();
    [DllImport(OPENGL)] public static extern void glPushMatrix();
    [DllImport(OPENGL)] public static extern void glPopMatrix();
    [DllImport(OPENGL)] public static extern void glTranslatef(float x, float y, float z);
    [DllImport(OPENGL)] public static extern void glRotatef(float angle, float x, float y, float z);
    [DllImport(OPENGL)] public static extern void glScalef(float x, float y, float z);
    [DllImport(OPENGL)] public static extern void glViewport(int x, int y, int width, int height);
    [DllImport(OPENGL)] public static extern void glFrustum(double l, double r, double b, double t, double n, double f);
    [DllImport(OPENGL)] public static extern void glDepthFunc(uint func);
    [DllImport(OPENGL)] public static extern void glDepthMask(byte flag);
    [DllImport(OPENGL)] public static extern void glShadeModel(uint mode);
    [DllImport(OPENGL)] public static extern void glBlendFunc(uint sfactor, uint dfactor);
    [DllImport(OPENGL)] public static extern void glLineWidth(float width);
    [DllImport(OPENGL)] public static extern void glPointSize(float size);
    [DllImport(OPENGL)] public static extern void glHint(uint target, uint mode);
    [DllImport(OPENGL)] public static extern void glFlush();
    [DllImport(OPENGL)] public static extern void glLightfv(uint light, uint pname, float[] p);
    [DllImport(OPENGL)] public static extern void glLightModelfv(uint pname, float[] p);
    [DllImport(OPENGL)] public static extern void glMaterialfv(uint face, uint pname, float[] p);
    [DllImport(OPENGL)] public static extern void glMaterialf(uint face, uint pname, float param);
    [DllImport(OPENGL)] public static extern void glColorMaterial(uint face, uint mode);
    [DllImport(OPENGL)] public static extern uint glGenLists(int range);
    [DllImport(OPENGL)] public static extern void glNewList(uint list, uint mode);
    [DllImport(OPENGL)] public static extern void glEndList();
    [DllImport(OPENGL)] public static extern void glCallList(uint list);
    [DllImport(OPENGL)] public static extern IntPtr wglCreateContext(IntPtr hdc);
    [DllImport(OPENGL)] public static extern bool wglMakeCurrent(IntPtr hdc, IntPtr hglrc);
    [DllImport(OPENGL)] public static extern bool wglDeleteContext(IntPtr hglrc);
    [DllImport(GDI)] public static extern int ChoosePixelFormat(IntPtr hdc, ref PIXELFORMATDESCRIPTOR pfd);
    [DllImport(GDI)] public static extern bool SetPixelFormat(IntPtr hdc, int format, ref PIXELFORMATDESCRIPTOR pfd);
    [DllImport(GDI)] public static extern bool SwapBuffers(IntPtr hdc);
    [DllImport(USER)] public static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport(USER)] public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
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
