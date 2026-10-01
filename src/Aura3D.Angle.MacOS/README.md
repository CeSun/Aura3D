# Aura3D.Angle.MacOS

ANGLE (Metal backend) macOS dylibs plus the IOSurface helper for the Aura3D.Avalonia render path.

## What's inside

| file | purpose |
| --- | --- |
| `native/macos-arm64/libEGL.dylib` / `libGLESv2.dylib` | ANGLE GLES 3.0 over Metal, built from commit `58f8882` (`target_os=mac`, `angle_enable_metal`) |
| `native/macos-arm64/libAura3dAngleMacOSHelper.dylib` | tiny C shim that creates 'BGRA' IOSurfaces (row stride 64-byte aligned) — `IOSurfaceCreate` P/Invoked directly from a .NET process crashes in objc class realization on macOS 26 |
| `build/` + `buildTransitive/Aura3D.Angle.MacOS.targets` | copies the three dylibs into the consumer's output directory; `MacAngleNative` loads them from `AppContext.BaseDirectory` |

## Why

macOS 26's OpenGL (the GL-on-Metal translation layer, driver string `4.1 Metal - 90.5`) does not implement point
primitives at all: `ALIASED_POINT_SIZE_RANGE` reports `[0, 0]` and any point draw is culled, in both core and
legacy profiles. Point clouds therefore render as invisible dust under the native driver. Aura3D's macOS desktop
hosts keep the Avalonia OpenGL compositor but render 3D viewports through this self-hosted ANGLE(Metal) context
(see `Aura3DViewBase.MacAngle.cs` / `MacAngleBackend` in Aura3D.Avalonia).

## Rebuilding the slice

```bash
src/Aura3D.Angle.MacOS/build-angle-macos.sh
```

Requires Xcode + depot_tools in PATH (`gn`/`ninja`/`gclient`); on Xcode 26 also
`xcodebuild -downloadComponent MetalToolchain`. The script reuses the `~/angle_ios` checkout by default
(same ANGLE revision as the iOS slice) and rebuilds the C shim from this repository.

License: the binaries are ANGLE, BSD-3-Clause (`LICENSE.angle.txt`).
