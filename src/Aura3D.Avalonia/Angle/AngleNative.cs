#if ANGLE_HOST
using System.Runtime.InteropServices;

namespace Aura3D.Avalonia.Angle;

/// <summary>
/// ANGLE 桥接所需的最小原生入口声明。libEGL/libGLESv2（ANGLE 构建）由应用工程以
/// NativeReference 方式静态链接，`__Internal` 在主程序符号表中解析，可避开 Apple 的
/// OpenGLES.framework 桩。禁止在这里加入经 Apple GL 桩也能解析的符号，防止后端混用。
/// </summary>
internal static class AngleNative
{
    private const string Lib = "__Internal";

    // ---- EGL ----
    [DllImport(Lib)] public static extern IntPtr eglGetError();

    [DllImport(Lib)] public static extern bool eglInitialize(IntPtr dpy, out int major, out int minor);

    [DllImport(Lib)] public static extern IntPtr eglQueryString(IntPtr dpy, int name);

    [DllImport(Lib)] public static extern bool eglBindAPI(int api);

    [DllImport(Lib)] public static extern bool eglChooseConfig(IntPtr dpy, int[] attribList, out IntPtr config,
        int bufSize, out int n);

    [DllImport(Lib)] public static extern IntPtr eglCreateContext(IntPtr dpy, IntPtr config, IntPtr shareCtx,
        int[] attribList);

    [DllImport(Lib)] public static extern IntPtr eglCreatePbufferSurface(IntPtr dpy, IntPtr config, int[] attribList);

    [DllImport(Lib)] public static extern bool eglMakeCurrent(IntPtr dpy, IntPtr draw, IntPtr read, IntPtr ctx);

    [DllImport(Lib)] public static extern IntPtr eglGetProcAddress(string procName);

    [DllImport(Lib)] public static extern IntPtr eglGetPlatformDisplayEXT(int platform, IntPtr nativeDisplay,
        int[]? attribList);

    [DllImport(Lib)] public static extern IntPtr eglCreateImageKHR(IntPtr dpy, IntPtr ctx, int target,
        IntPtr buffer, int[]? attribList);

    [DllImport(Lib)] public static extern bool eglDestroyImageKHR(IntPtr dpy, IntPtr image);

    // ---- GLES（仅宿主自身使用的子集；管线内部走 Silk.NET + GetProcAddress）----
    [DllImport(Lib)] public static extern IntPtr glGetString(int name);

    [DllImport(Lib)] public static extern void glBindFramebuffer(int target, uint framebuffer);

    [DllImport(Lib)] public static extern void glGenFramebuffers(int n, out uint framebuffers);

    [DllImport(Lib)] public static extern void glDeleteFramebuffers(int n, ref uint framebuffers);

    [DllImport(Lib)] public static extern void glGenTextures(int n, out uint textures);

    [DllImport(Lib)] public static extern void glDeleteTextures(int n, ref uint textures);

    [DllImport(Lib)] public static extern void glBindTexture(int target, uint texture);

    [DllImport(Lib)] public static extern void glFramebufferTexture2D(int target, int attachment,
        int textarget, uint texture, int level);

    [DllImport(Lib)] public static extern int glCheckFramebufferStatus(int target);

    [DllImport(Lib)] public static extern int glGetError();

    [DllImport(Lib)] public static extern void glFinish();

    [DllImport(Lib)] public static extern void glEGLImageTargetTexture2DOES(int target, IntPtr image);

    // eglGetProcAddress 只覆盖扩展入口；核心符号经 dlsym(RTLD_DEFAULT) 兜底。
    [DllImport(Lib)] public static extern IntPtr dlsym(IntPtr handle, string symbol);
}
#endif
