# Aura3D.Angle.iOS

English | [中文](./README_CN.md)

[Aura3D](https://github.com/CeSun/Aura3d) ships its own ANGLE (Metal backend) context on iOS,
but the ANGLE native libraries must be linked into the main executable by the **application**:
the bridging code in `Aura3D.Avalonia` resolves those symbols through `DllImport("__Internal")`
(to avoid mixing with the OpenGLES symbols Apple provides), so the libraries cannot live only
inside a library package. This package does that job on behalf of the application: once installed,
its targets inject the `NativeReference` automatically and no configuration is needed.

## Usage

There is nothing to install separately. The iOS target of `Aura3D.Avalonia` depends on this package
with an exact version range, and the `buildTransitive` targets inside the package flow through to the
application project, so the app side does not need a single line of ANGLE configuration:

```shell
dotnet add package Aura3D.Avalonia
```

Referencing this package directly is also fine if you want explicit control over the slice version:

```shell
dotnet add package Aura3D.Angle.iOS
```

It only takes effect on iOS target frameworks (`$(TargetFramework)` contains `-ios`); installing it in
desktop or Android projects has no side effects. After installation, still make sure the host keeps the
default Metal compositor on iOS (do not force `iOSRenderingMode.OpenGl`) — see
[Platforms and Render Backends](../../doc/en/platform-render-backends.md) in the repository docs.

## Package contents

```
native/iossimulator-arm64/libEGL.framework      simulator slice
native/iossimulator-arm64/libGLESv2.framework
native/ios-arm64/libEGL.framework               device slice
native/ios-arm64/libGLESv2.framework
build/Aura3D.Angle.iOS.targets                  picks a slice by $(RuntimeIdentifier) and injects NativeReference
buildTransitive/Aura3D.Angle.iOS.targets        same as above (hit when consumed transitively)
LICENSE.angle.txt                               verbatim BSD-3-Clause text from ANGLE
```

Both slices are committed with the package (about 24 MB), so publishing needs neither depot_tools nor
Xcode. A missing slice fails the build outright — it never silently produces a package that
"compiles but renders nothing at runtime".

## Publishing

This package has no publishing channel of its own; it rides the `pack.yml` release train: within a single
run it is packed into `local-feed/` first (used by the repository's own restore, `NuGet.config` declares
that directory as a package source), then packed into `packages/` and pushed to nuget.org together with
the other libraries (secret `NUGET_API_KEY`). Since everything comes from the same commit, the version
pinned in the `Aura3D.Avalonia` nuspec is guaranteed to be the one published by that very run.

A slice hotfix means bumping `<Version>` in `Aura3D.Angle.iOS.csproj` in this directory **and** bumping
the range in `Directory.Packages.props` **in the same commit**, then running `pack.yml` once. They must be
changed as a pair because `Aura3D.Avalonia` pins an exact range `[<version>]`, and a slice is ABI-paired
with the `DllImport` signatures inside `Aura3D.Avalonia` — consumers must not be passively upgraded to a
slice that was never tested as a matching pair. In other words, changing a slice means re-publishing
`Aura3D.Avalonia`.

With no slice at all under `native/`, `dotnet pack` fails immediately (`AngleNativeCheck`) instead of
emitting an empty shell package.

## Rebuilding the slices

```shell
./build-angle-ios.sh            # simulator slice
./build-angle-ios.sh --device   # additionally the device slice
dotnet pack -c Release -o local-feed
```

The script drops the frameworks into `native/`. The current version corresponds to ANGLE
`58f8882372e8a4e83da821ac5d16f0323c3fa1af` with gn arguments `angle_enable_metal=true`,
`is_debug=false`, `enable_rust=false` (a standalone checkout is enough, no full Chromium checkout);
the device slice additionally needs `ios_enable_code_signing=false`. The slices are built with ANGLE's
default `ios_deployment_target`, with `minos` at 18.0.

## Verification status

- Simulator arm64: verified. With `Example.iOS` dropping its local path reference and relying solely on
  this package, `otool -L` shows `@rpath/libEGL.framework/libEGL`, and both the Base Geometries and
  PBR RenderPipeline pages render correctly.
- Device arm64: **runtime not verified**. No device was available; the slice builds and is injected
  correctly for the `ios-arm64` RID, but real rendering on an actual device has never been exercised.

## License

The binaries inside the package come from ANGLE and are distributed under ANGLE's own BSD-3-Clause
license (see `LICENSE.angle.txt` for the verbatim text).
