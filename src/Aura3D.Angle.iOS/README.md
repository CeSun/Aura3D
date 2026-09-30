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

This package has its own publishing channel: trigger `build-ios-lib.yml` manually and type the version into
the dispatch form. The job packs with `-p:Version=<input>`, and a global MSBuild property outranks the
literal `<Version>` in this directory's csproj, so any version can be published without touching a
repository file. That single run packs the nupkg, confirms the version inside it is the one you entered,
checks line by line that both slices and the `build/` + `buildTransitive/` targets are inside it, and pushes
to nuget.org with a temporary API key obtained through GitHub OIDC (secret `NUGET_USER` plus the
`environment: production` trusted-publishing policy). Clearing the `publish` checkbox stops at the artifact:
verify the package without releasing it. It no longer rides the `pack.yml` release train: the published
artifact set no longer contains it, and the repository's own restore resolves it from nuget.org through the
exact range the main package pins. The browser-side
`Aura3D.Avalonia.Browser` works the same way through `build-browser-lib.yml`.

The version now lives in two places that no longer reference each other: `<Version>` in this csproj is what
an in-repo pack without `-p` produces, and `Aura3DAngleIosVersion` in `Directory.Packages.props` only feeds
the exact range `Aura3D.Avalonia` pins. A slice hotfix means running `build-ios-lib.yml` with a version
nuget.org does not serve yet → then setting **both literals in the same commit** to the version you just
published → then running `pack.yml` to re-publish `Aura3D.Avalonia`. Each way of forgetting has its own
consequence, and the workflow tells them apart: the two literals disagreeing means the slice sitting in this
repository is not the one consumers resolve from nuget.org, and any later pack without `-p` produces the
wrong number — that job treats it as an error; skipping only the props side leaves the main package pinned to
the old slice, a warning. They must agree because `Aura3D.Avalonia` pins an exact range `[<version>]`, and a slice is
ABI-paired with the `DllImport` signatures inside `Aura3D.Avalonia` — consumers must not be passively
upgraded to a slice that was never tested as a matching pair. In other words, changing a slice means
re-publishing `Aura3D.Avalonia`.

The publish job refuses a version nuget.org already serves, because `--skip-duplicate` would silently skip
the stale package and that green means nothing — so an input version that is already live fails the run
instead of quietly publishing nothing. With no slice at all under `native/`, `dotnet pack` fails immediately
(`AngleNativeCheck`) instead of emitting an empty shell package; a package missing only *one* of the two
slices still packs, which is what the content check in `build-ios-lib.yml` catches.

## Rebuilding the slices

```shell
./build-angle-ios.sh            # simulator slice
./build-angle-ios.sh --device   # additionally the device slice
```

The script drops the frameworks into `native/`. The current version corresponds to ANGLE
`58f8882372e8a4e83da821ac5d16f0323c3fa1af` with gn arguments `angle_enable_metal=true`,
`is_debug=false`, `enable_rust=false` (a standalone checkout is enough, no full Chromium checkout);
the device slice additionally needs `ios_enable_code_signing=false`. The slices are built with ANGLE's
default `ios_deployment_target`, with `minos` at 18.0.

## Verification status

- Simulator arm64: verified. With `Aura3D.Gallery.iOS` dropping its local path reference and relying solely on
  this package, `otool -L` shows `@rpath/libEGL.framework/libEGL`, and both the Base Geometries and
  PBR RenderPipeline pages render correctly.
- Device arm64: **runtime not verified**. No device was available; the slice builds and is injected
  correctly for the `ios-arm64` RID, but real rendering on an actual device has never been exercised.

## License

The binaries inside the package come from ANGLE and are distributed under ANGLE's own BSD-3-Clause
license (see `LICENSE.angle.txt` for the verbatim text).
