# Platforms and Render Backends

`Aura3DView` obtains its GL context differently per platform. This document describes which path each platform takes, what iOS additionally requires, and the pitfalls to avoid when writing custom passes against the GLES subset.

## Render backend per platform

| Platform | Host renderer | How Aura3D gets a GL context | Application-side configuration |
|---|---|---|---|
| Windows / Linux | Desktop OpenGL | Avalonia `OpenGlControlBase` | None |
| Android | OpenGL ES | Avalonia `OpenGlControlBase` | None |
| macOS | OpenGL | Avalonia `OpenGlControlBase` | No platform special-casing; if the viewport stays blank, copy the explicit `AvaloniaNativeRenderingMode.OpenGl` from `Example.Desktop/Program.cs` |
| iOS | Metal (Avalonia default) | **Self-hosted ANGLE(Metal) context composed through a Skia lease**; falls back to `OpenGlControlBase` when the host explicitly selects OpenGl | No platform special-casing, but the ANGLE iOS frameworks must be linked |

The two paths live in `src/Aura3D.Avalonia/Aura3DViewBase.OpenGl.cs` and `Aura3DViewBase.Angle.cs`; the shared flow is in `Aura3DViewBase.cs`. The `Aura3DView` API an application uses is identical on every platform.

## iOS: why a self-hosted ANGLE context

Avalonia's iOS host defaults to the Metal compositor, and in that mode Avalonia offers no GL interop — `OpenGlControlBase` silently fails to initialize (blank white surface, no exception). The previous workaround was to force the whole host application onto `iOSRenderingMode.OpenGl`, which pinned the app to Apple's deprecated EAGL backend.

The iOS target of `Aura3D.Avalonia` now carries a second path: it creates its own ANGLE(Metal) EGL context, renders into it, and composites the resulting `MTLTexture` onto the screen through a zero-copy Skia lease. The host application no longer has to give up anything about its rendering mode.

Both paths are compiled into one binary and ownership is decided once, on the first frame:

- **Authoritative signal** — `OpenGlControlBase.OnOpenGlInit` is called, which means the host enabled OpenGL mode; the ANGLE branch stands down entirely and Avalonia drives rendering.
- **Secondary signal** — the Skia lease's `GRContext.Backend` is read once: `Metal` means ANGLE takes over, anything else hands control back to `OpenGlControlBase`.

The decision is logged as `[aura3d-angle] compositor backend=..., path=...` — check that line first when debugging.

## iOS: where the ANGLE frameworks come from

`Aura3D.Avalonia` resolves ANGLE's EGL/GLES2 symbols through `DllImport("__Internal")` (to avoid clashing with Apple's own OpenGLES symbols), so the two frameworks have to be linked into the **application** executable rather than living inside the class library.

**That part is now automatic.** `Aura3D.Avalonia`'s iOS target depends on `Aura3D.Angle.iOS`, and the package's `buildTransitive` targets pick the slice by `$(RuntimeIdentifier)` and inject the `NativeReference` items into the app project — an app needs zero ANGLE configuration, and `example/Example.iOS` has none. The dependency is pinned to an exact range (`[0.1.0]`): the slice and the P/Invoke signatures are an ABI pair, so a consumer must never be silently upgraded onto a slice that was not tested against them. Replacing the slices therefore means bumping that version and releasing `Aura3D.Avalonia` again.

```shell
dotnet add package Aura3D.Avalonia   # pulls Aura3D.Angle.iOS in on iOS
```

The package ships two ANGLE slices, `iossimulator-arm64` and `ios-arm64`, and rides the `pack.yml` release train together with the other libraries (same commit, so the pinned version is by construction the one just published). There is no separate channel for hot-fixing a slice: bump `<Version>` here, bump the range in `Directory.Packages.props`, and run `pack.yml`. A missing slice fails the build loudly instead of quietly producing a blank viewport.

Inside this repository you have to produce that package once first (`NuGet.config` declares `local-feed/` as a package source):

```shell
dotnet pack src/Aura3D.Angle.iOS -c Release -o local-feed
```

Every CI job does the same thing before it restores. **Manual route** (when you build ANGLE yourself):

1. Build ANGLE for iOS from a standalone ANGLE checkout (not a Chromium checkout) with the Metal backend enabled; the only required gn argument is `enable_rust=false`. Drop the resulting `libEGL.framework` and `libGLESv2.framework` into `src/Aura3D.Angle.iOS/native/iossimulator-arm64/` and `native/ios-arm64/` (both slices are committed to the repository and packed as-is). `src/Aura3D.Angle.iOS/build-angle-ios.sh --device` codifies this; the device slice additionally needs `ios_enable_code_signing = false`, otherwise `gn gen` fails while looking for an "Apple Development" identity — which is exactly the situation in a certificate-free CI.
2. Adding `<PackageReference Include="Aura3D.Angle.iOS" />` to your own app works just as well as getting it transitively from `Aura3D.Avalonia`.
3. Do not guard such an `ItemGroup` with an `Exists(...)` condition: when the frameworks are missing the group is skipped silently, the app still compiles, and the viewport stays blank because the ANGLE session fails. Verify the frameworks really were linked — check that they appear under `<App>.app/Frameworks/`.
4. Hardware requirements: ANGLE's Metal backend needs Metal GPU family 4 (A11 or later); tvOS is not supported. The committed slices are built with ANGLE's default `ios_deployment_target`, i.e. `minos` 18.0, so an app deployment target below that produces a linker version mismatch warning.

## The GLES 3.0 subset: constraints for custom passes

Shaders and GL calls are written against a plain OpenGL ES 3.0 subset so that one pipeline works on desktop GL, Android GLES and ANGLE on iOS. Some of these constraints bite for real under ANGLE:

- ANGLE's Metal backend only exposes ES 3.0 — requesting an ES 3.1/3.2 context fails with `EGL_BAD_MATCH` — and it does not provide `GL_EXT_float_blend`. **Drawing with blending enabled into a 32-bit float color attachment is treated as `GL_INVALID_OPERATION` and the entire draw is dropped silently**; the symptom is a black image, not an error. The framework's HDR render targets are therefore all `Rgba16f`; do not allocate an `Rgba32f` color attachment and then enable blending in a custom pass.
- Depth attachments `DEPTH_COMPONENT16/24/32F` and `DEPTH24_STENCIL8`/`DEPTH32F_STENCIL8` are ES 3.0 core and work fine under ANGLE — the restriction above concerns color attachments only.
- No compute. 3D textures cannot be imported as external textures.
- ANGLE translates GLSL to MSL and compiles it at runtime; there is no on-disk binary cache. Measured first-frame cost on iOS (iPhone 17 simulator / iOS 27.0): roughly 5.6–5.9 s for the PBR and cascaded-shadow pages, 0.4–0.7 s for simple scenes.

## Frame scheduling and thread semantics

- `RequestNextFrameRendering()` keeps the same signature on both backends. With `AutoRequestNextFrameRendering = false` the application requests each frame itself.
- On iOS the frame callback runs on the compositor render thread, unlike desktop where the callback is already on the UI thread. `SceneInitialized` / `SceneUpdated` / `ContextLost` / `ContextRestored` are marshalled back to the UI thread, so handlers can touch the control safely.
- Detaching the control from the visual tree destroys the context and actually deletes GL objects, returning VRAM. Scene, nodes and CPU-side resources are preserved, and re-attaching raises `ContextRestored` rather than `SceneInitialized` again. This matches desktop `OnOpenGlDeinit` semantics; see [GPU Resource Lifecycle](./gpu-resource-lifecycle.md) for details.

## Known issues on the GL (EAGL) fallback path

This path is only used when the host explicitly sets `iOSRenderingMode.OpenGl` (Avalonia `OpenGlControlBase` on Apple EAGL). Default iOS configurations never reach it. Current knowledge comes from simulator runs and has not been verified on a device:

- Pages that use the PBR deferred pipeline take minutes per frame — the simulator's EAGL is a translated/software path and cannot carry several large float render targets plus cascaded shadows. On-device EAGL is hardware-driven and may behave differently.
- On the cascaded shadow maps example page, the ground plane stops producing fragments from the third frame on, while a minimal program that only consumes `gl_VertexID` still lands in the same frame — which points at a silently dropped draw inside the simulator's GLES-on-Metal layer.

Bottom line: stay on the default (ANGLE) path on iOS; there is no reason to force OpenGL mode anymore.
