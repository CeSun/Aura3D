#if WEBGL_HOST
using System.Runtime.InteropServices;

namespace Aura3D.Avalonia.WebGL;

/// <summary>
/// Browser 桥接所需的最小原生入口声明。wasm 应用经 WasmBuildNative 把
/// SkiaSharp.NativeAssets.WebAssembly 的 libSkiaSharp.a（内含 emscripten GLES/EGL shim，
/// GLES→WebGL2）静态链接进主模块，符号按 "libSkiaSharp" 解析——与 Avalonia.Browser 自身
/// WebGlContext 的用法一致。禁止在这里加入其他库名的符号，防止后端混用。
/// </summary>
internal static class WebGlNative
{
    private const string Lib = "libSkiaSharp";

    // ---- EGL（仅入口点解析；上下文由 Avalonia 合成器持有，本会话不自建）----
    [DllImport(Lib)] public static extern IntPtr eglGetProcAddress(string procName);

    // ---- GLES（仅宿主自身使用的子集；管线内部走 Silk.NET + GetProcAddress）----
    [DllImport(Lib)] public static extern IntPtr glGetString(int name);

    [DllImport(Lib)] public static extern void glBindFramebuffer(int target, uint framebuffer);

    [DllImport(Lib)] public static extern void glGenFramebuffers(int n, out uint framebuffers);

    [DllImport(Lib)] public static extern void glDeleteFramebuffers(int n, ref uint framebuffers);

    [DllImport(Lib)] public static extern void glGenTextures(int n, out uint textures);

    [DllImport(Lib)] public static extern void glDeleteTextures(int n, ref uint textures);

    [DllImport(Lib)] public static extern void glBindTexture(int target, uint texture);

    [DllImport(Lib)] public static extern void glTexParameteri(int target, int pname, int param);

    [DllImport(Lib)] public static extern void glTexImage2D(int target, int level, int internalFormat,
        int width, int height, int border, int format, int type, IntPtr pixels);

    [DllImport(Lib)] public static extern void glFramebufferTexture2D(int target, int attachment,
        int textarget, uint texture, int level);

    [DllImport(Lib)] public static extern int glCheckFramebufferStatus(int target);

    [DllImport(Lib)] public static extern int glGetError();

    [DllImport(Lib)] public static extern void glFinish();
}
#endif
