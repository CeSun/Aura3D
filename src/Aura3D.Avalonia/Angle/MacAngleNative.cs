using System.Runtime.InteropServices;

namespace Aura3D.Avalonia.Angle;

/// <summary>
/// 形态 B（macOS 保持 GL 合成器，自持 ANGLE-Metal 会话经 IOSurface 桥进合成）所需的
/// 原生入口懒加载封装。三组来源：
///   libEGL / libGLESv2 —— ANGLE 的 macOS 切片，随应用目录分发（AppContext.BaseDirectory，
///   dotnet run 与 .app 两种布局都成立；@executable_path 在 dotnet run 下指向 dotnet 本体，不可用）；
///   IOSurface / OpenGL(CGL) —— 系统框架，合成侧把 IOSurface 导入 Avalonia 的 NSOpenGL 上下文；
///   CoreFoundation —— 构造 IOSurface 的属性字典（kIOSurface* 键是框架导出的 CFString，直接 dlsym）。
/// 全部经 <see cref="NativeLibrary"/> 解析为函数指针：非 macOS 平台零绑定成本，也不触发
/// AOT 对 DllImport 的早期绑定；解析失败集中在首次调用时上报。
/// </summary>
internal static unsafe class MacAngleNative
{
    private static readonly object Gate = new();
    private static bool _resolved;
    private static bool _failed;

    private static nint _egl;
    private static nint _gles2;
    private static nint _ioSurfaceLib;
    private static nint _openGlLib;
    private static nint _helper;

    /// <summary>解析失败原因，供宿主诊断。</summary>
    public static string? LoadError { get; private set; }

    private static bool EnsureLoaded()
    {
        lock (Gate)
        {
            if (_resolved)
                return !_failed;

            _resolved = true;

            if (!OperatingSystem.IsMacOS())
                return Fail("macOS only");

            if (!TryLoadAll())
                _failed = true;

            return !_failed;
        }
    }

    private static bool TryLoadAll()
    {
        if (!NativeLibrary.TryLoad(Path.Combine(AppContext.BaseDirectory, "libEGL.dylib"), out _egl) &&
            !NativeLibrary.TryLoad("libEGL.dylib", out _egl))
            return Fail("libEGL.dylib not found next to the app; ANGLE macOS slice missing");

        if (!NativeLibrary.TryLoad(Path.Combine(AppContext.BaseDirectory, "libGLESv2.dylib"), out _gles2) &&
            !NativeLibrary.TryLoad("libGLESv2.dylib", out _gles2))
            return Fail("libGLESv2.dylib not found next to the app");

        if (!NativeLibrary.TryLoad("/System/Library/Frameworks/IOSurface.framework/IOSurface", out _ioSurfaceLib))
            return Fail("IOSurface framework not loadable");

        if (!NativeLibrary.TryLoad("/System/Library/Frameworks/OpenGL.framework/OpenGL", out _openGlLib))
            return Fail("OpenGL framework not loadable");

        // IOSurface 的属性字典要过 CoreFoundation/ObjC——macOS 26 上 dotnet 进程内直接
        // P/Invoke IOSurfaceCreate 会在 objc 类实现时 SIGBUS（C 调用正常），所以收进 C shim。
        if (!NativeLibrary.TryLoad(Path.Combine(AppContext.BaseDirectory, "libAura3dAngleMacOSHelper.dylib"),
                out _helper) &&
            !NativeLibrary.TryLoad("libAura3dAngleMacOSHelper.dylib", out _helper))
            return Fail("libAura3dAngleMacOSHelper.dylib not found next to the app");

        return true;
    }

    private static bool TrySym(nint lib, string name, out nint address) =>
        NativeLibrary.TryGetExport(lib, name, out address);

    private static bool Fail(string message)
    {
        LoadError = message;
        Console.WriteLine("[aura3d-macangle] FAIL " + message);
        return false;
    }

    private static nint Sym(nint lib, string name)
    {
        if (!NativeLibrary.TryGetExport(lib, name, out var p))
            throw new EntryPointNotFoundException($"[aura3d-macangle] missing export {name}");
        return p;
    }

    // ---- EGL（ANGLE libEGL）----

    internal static delegate* unmanaged<int, nint, int*, nint> EglGetPlatformDisplayEXT;
    internal static delegate* unmanaged<nint, int*, int*, byte> EglInitialize;
    internal static delegate* unmanaged<int> EglGetError;
    internal static delegate* unmanaged<nint, int, nint> EglQueryString;
    internal static delegate* unmanaged<int, byte> EglBindAPI;
    internal static delegate* unmanaged<nint, int*, nint*, int, int*, byte> EglChooseConfig;
    internal static delegate* unmanaged<nint, nint, nint, int*, nint> EglCreateContext;
    internal static delegate* unmanaged<nint, nint, int*, nint> EglCreatePbufferSurface;
    internal static delegate* unmanaged<nint, int, nint, nint, int*, nint> EglCreatePbufferFromClientBuffer;
    internal static delegate* unmanaged<nint, nint, nint, nint, byte> EglMakeCurrent;
    internal static delegate* unmanaged<nint, nint, int, nint, int*, nint> EglCreateImageKHR;
    internal static delegate* unmanaged<nint, nint, byte> EglDestroyImageKHR;
    internal static delegate* unmanaged<nint, nint, byte> EglDestroySurface;
    internal static delegate* unmanaged<nint, nint, byte> EglDestroyContext;
    internal static delegate* unmanaged<byte*, nint> EglGetProcAddress;

    // ---- GLES（ANGLE libGLESv2）----

    internal static delegate* unmanaged<int, byte*> GlGetString;
    internal static delegate* unmanaged<int> GlGetError;
    internal static delegate* unmanaged<void> GlFinish;
    internal static delegate* unmanaged<int, uint*, void> GlGenTextures;
    internal static delegate* unmanaged<int, uint*, void> GlDeleteTextures;
    internal static delegate* unmanaged<int, uint, void> GlBindTexture;
    internal static delegate* unmanaged<int, uint*, void> GlGenFramebuffers;
    internal static delegate* unmanaged<int, uint*, void> GlDeleteFramebuffers;
    internal static delegate* unmanaged<int, uint, void> GlBindFramebuffer;
    internal static delegate* unmanaged<int, int, int, uint, int, void> GlFramebufferTexture2D;
    internal static delegate* unmanaged<int, int> GlCheckFramebufferStatus;
    internal static delegate* unmanaged<int, nint, void> GlEGLImageTargetTexture2DOES;

    // ---- 合成侧：系统 OpenGL 框架（Avalonia 的 NSOpenGL 上下文）----

    internal static delegate* unmanaged<nint> CglGetCurrentContext;
    internal static delegate* unmanaged<nint, uint, uint, int, int, uint, uint, nint, uint, int> CglTexImageIOSurface2D;
    internal static delegate* unmanaged<int, uint*, void> GlGenTexturesNative;
    internal static delegate* unmanaged<uint, uint, void> GlBindTextureNative;
    internal static delegate* unmanaged<int, uint*, void> GlDeleteTexturesNative;
    internal static delegate* unmanaged<uint, int*, void> GlGetIntegervNative;
    internal static delegate* unmanaged<uint, void> GlActiveTextureNative;
    internal static delegate* unmanaged<void> GlFlushNative;
    internal static delegate* unmanaged<int> GlGetErrorNative;

    // ---- IOSurface shim（C 侧创建，绕开 dotnet 进程的 objc 类实现缺陷）----

    internal static delegate* unmanaged<int, int, nint> IoSurfaceCreateBgra;
    internal static delegate* unmanaged<nint, void> IoSurfaceRelease;

    /// <summary>解析全部符号。失败返回 false，错误在 <see cref="LoadError"/>。</summary>
    internal static bool Resolve()
    {
        if (!EnsureLoaded())
            return false;

        EglGetPlatformDisplayEXT = (delegate* unmanaged<int, nint, int*, nint>)Sym(_egl, "eglGetPlatformDisplayEXT");
        EglInitialize = (delegate* unmanaged<nint, int*, int*, byte>)Sym(_egl, "eglInitialize");
        EglGetError = (delegate* unmanaged<int>)Sym(_egl, "eglGetError");
        EglQueryString = (delegate* unmanaged<nint, int, nint>)Sym(_egl, "eglQueryString");
        EglBindAPI = (delegate* unmanaged<int, byte>)Sym(_egl, "eglBindAPI");
        EglChooseConfig = (delegate* unmanaged<nint, int*, nint*, int, int*, byte>)Sym(_egl, "eglChooseConfig");
        EglCreateContext = (delegate* unmanaged<nint, nint, nint, int*, nint>)Sym(_egl, "eglCreateContext");
        EglCreatePbufferSurface = (delegate* unmanaged<nint, nint, int*, nint>)Sym(_egl, "eglCreatePbufferSurface");
        EglCreatePbufferFromClientBuffer = (delegate* unmanaged<nint, int, nint, nint, int*, nint>)
            Sym(_egl, "eglCreatePbufferFromClientBuffer");
        EglMakeCurrent = (delegate* unmanaged<nint, nint, nint, nint, byte>)Sym(_egl, "eglMakeCurrent");
        EglCreateImageKHR = (delegate* unmanaged<nint, nint, int, nint, int*, nint>)Sym(_egl, "eglCreateImageKHR");
        EglDestroyImageKHR = (delegate* unmanaged<nint, nint, byte>)Sym(_egl, "eglDestroyImageKHR");
        EglDestroySurface = (delegate* unmanaged<nint, nint, byte>)Sym(_egl, "eglDestroySurface");
        EglDestroyContext = (delegate* unmanaged<nint, nint, byte>)Sym(_egl, "eglDestroyContext");
        EglGetProcAddress = (delegate* unmanaged<byte*, nint>)Sym(_egl, "eglGetProcAddress");

        GlGetString = (delegate* unmanaged<int, byte*>)Sym(_gles2, "glGetString");
        GlGetError = (delegate* unmanaged<int>)Sym(_gles2, "glGetError");
        GlFinish = (delegate* unmanaged<void>)Sym(_gles2, "glFinish");
        GlGenTextures = (delegate* unmanaged<int, uint*, void>)Sym(_gles2, "glGenTextures");
        GlDeleteTextures = (delegate* unmanaged<int, uint*, void>)Sym(_gles2, "glDeleteTextures");
        GlBindTexture = (delegate* unmanaged<int, uint, void>)Sym(_gles2, "glBindTexture");
        GlGenFramebuffers = (delegate* unmanaged<int, uint*, void>)Sym(_gles2, "glGenFramebuffers");
        GlDeleteFramebuffers = (delegate* unmanaged<int, uint*, void>)Sym(_gles2, "glDeleteFramebuffers");
        GlBindFramebuffer = (delegate* unmanaged<int, uint, void>)Sym(_gles2, "glBindFramebuffer");
        GlFramebufferTexture2D = (delegate* unmanaged<int, int, int, uint, int, void>)Sym(_gles2, "glFramebufferTexture2D");
        GlCheckFramebufferStatus = (delegate* unmanaged<int, int>)Sym(_gles2, "glCheckFramebufferStatus");
        GlEGLImageTargetTexture2DOES = (delegate* unmanaged<int, nint, void>)Sym(_gles2, "glEGLImageTargetTexture2DOES");

        CglGetCurrentContext = (delegate* unmanaged<nint>)Sym(_openGlLib, "CGLGetCurrentContext");
        CglTexImageIOSurface2D = (delegate* unmanaged<nint, uint, uint, int, int, uint, uint, nint, uint, int>)
            Sym(_openGlLib, "CGLTexImageIOSurface2D");
        GlGenTexturesNative = (delegate* unmanaged<int, uint*, void>)Sym(_openGlLib, "glGenTextures");
        GlBindTextureNative = (delegate* unmanaged<uint, uint, void>)Sym(_openGlLib, "glBindTexture");
        GlDeleteTexturesNative = (delegate* unmanaged<int, uint*, void>)Sym(_openGlLib, "glDeleteTextures");
        GlGetIntegervNative = (delegate* unmanaged<uint, int*, void>)Sym(_openGlLib, "glGetIntegerv");
        GlActiveTextureNative = (delegate* unmanaged<uint, void>)Sym(_openGlLib, "glActiveTexture");
        GlFlushNative = (delegate* unmanaged<void>)Sym(_openGlLib, "glFlush");
        GlGetErrorNative = (delegate* unmanaged<int>)Sym(_openGlLib, "glGetError");

        IoSurfaceCreateBgra = (delegate* unmanaged<int, int, nint>)Sym(_helper, "aura3d_create_iosurface_bgra");
        IoSurfaceRelease = (delegate* unmanaged<nint, void>)Sym(_helper, "aura3d_release_iosurface");

        return true;
    }

}
