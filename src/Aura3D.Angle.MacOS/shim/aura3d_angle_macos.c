// 形态 B 的原生辅助：IOSurface 的创建必须经 CoreFoundation/ObjC 运行时，而 macOS 26 上
// dotnet 进程内直接 P/Invoke IOSurfaceCreate 会在 objc 类实现（__NSCFString realizeClass）
// 时 SIGBUS——同样的调用序列在 C 编译的代码里正常。因此把这一小块收进 C shim，
// 由 dotnet 侧以纯 C 函数调用。后续 Metal 纹理创建如遇同类问题也收进来。
#include <CoreFoundation/CoreFoundation.h>
#include <IOSurface/IOSurface.h>

void *aura3d_create_iosurface_bgra(int width, int height)
{
    CFMutableDictionaryRef dict = CFDictionaryCreateMutable(
        NULL, 0, &kCFTypeDictionaryKeyCallBacks, &kCFTypeDictionaryValueCallBacks);
    if (!dict)
    {
        fprintf(stderr, "[aura3d-shim] CFDictionaryCreateMutable failed\n");
        return NULL;
    }

    int bpe = 4;
    // IOSurface 的行跨距按 64 字节对齐，AllocSize 必须按对齐后的跨距计算，
    // 否则 IOSurfaceCreate 直接返回 NULL（表现为窗口宽度过 2048 后全部失败）。
    int bytesPerRow = ((width * bpe + 63) / 64) * 64;
    long allocSize = (long)bytesPerRow * (long)height;
    int pixelFormat = 'BGRA';

    CFNumberRef nw = CFNumberCreate(NULL, kCFNumberIntType, &width);
    CFNumberRef nh = CFNumberCreate(NULL, kCFNumberIntType, &height);
    CFNumberRef nb = CFNumberCreate(NULL, kCFNumberIntType, &bpe);
    CFNumberRef na = CFNumberCreate(NULL, kCFNumberLongType, &allocSize);
    CFNumberRef nbr = CFNumberCreate(NULL, kCFNumberIntType, &bytesPerRow);
    CFNumberRef np = CFNumberCreate(NULL, kCFNumberIntType, &pixelFormat);

    CFDictionarySetValue(dict, kIOSurfaceWidth, nw);
    CFDictionarySetValue(dict, kIOSurfaceHeight, nh);
    CFDictionarySetValue(dict, kIOSurfaceBytesPerElement, nb);
    CFDictionarySetValue(dict, kIOSurfaceBytesPerRow, nbr);
    CFDictionarySetValue(dict, kIOSurfaceAllocSize, na);
    CFDictionarySetValue(dict, kIOSurfacePixelFormat, np);

    IOSurfaceRef io = IOSurfaceCreate(dict);

    CFRelease(nw);
    CFRelease(nh);
    CFRelease(nb);
    CFRelease(na);
    CFRelease(nbr);
    CFRelease(np);
    CFRelease(dict);
    return io;
}

void aura3d_release_iosurface(void *io)
{
    if (io)
        CFRelease(io);
}

int aura3d_iosurface_width(void *io)
{
    return io ? (int)IOSurfaceGetWidth((IOSurfaceRef)io) : 0;
}
