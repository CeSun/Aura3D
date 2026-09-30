---
section: advanced
order: 3
---

# Platforms and Render Backends

`Aura3DView` obtains its GL context differently per platform. This document describes which path each platform takes, what iOS and browser additionally require, and the pitfalls to avoid when writing custom passes against the GLES subset.

## Render backend per platform

| Platform | Host renderer | How Aura3D gets a GL context | Application-side configuration |
|---|---|---|---|
| Windows / Linux | Desktop OpenGL | Avalonia `OpenGlControlBase` | None |
| Android | OpenGL ES | Avalonia `OpenGlControlBase` | None |
| macOS | OpenGL | Avalonia `OpenGlControlBase` | No platform special-casing; if the viewport stays blank, pin `AvaloniaNativeRenderingMode.OpenGl` explicitly in `AppBuilder` |
| iOS | Metal (Avalonia default) | **Self-hosted ANGLE(Metal) context composed through a Skia lease**; falls back to `OpenGlControlBase` when the host explicitly selects OpenGl | No platform special-casing, but the ANGLE iOS frameworks must be linked |
| Browser (wasm) | WebGL2 (Avalonia.Browser's Skia compositor) | **Borrows the compositor's WebGL2 context and composes through a Skia lease**; GLES 3.0 calls reach WebGL2 via the emscripten shim | Link switches are injected by the package; .NET 10 Release publishing must explicitly disable trimming, AOT and icall linking as described below |

The three paths live in `src/Aura3D.Avalonia/Aura3DViewBase.OpenGl.cs`, `Aura3DViewBase.Angle.cs` (iOS) and `Aura3DViewBase.WebGl.cs` (browser); the shared flow is in `Aura3DViewBase.cs`. The `Aura3DView` API an application uses is identical on every platform.

## iOS: why a self-hosted ANGLE context

Avalonia's iOS host defaults to the Metal compositor, and in that mode Avalonia offers no GL interop — `OpenGlControlBase` silently fails to initialize (blank white surface, no exception). The previous workaround was to force the whole host application onto `iOSRenderingMode.OpenGl`, which pinned the app to Apple's deprecated EAGL backend.

The iOS target of `Aura3D.Avalonia` now carries a second path: it creates its own ANGLE(Metal) EGL context, renders into it, and composites the resulting `MTLTexture` onto the screen through a zero-copy Skia lease. The host application no longer has to give up anything about its rendering mode.

Both paths are compiled into one binary and ownership is decided once, on the first frame:

- **Authoritative signal** — `OpenGlControlBase.OnOpenGlInit` is called, which means the host enabled OpenGL mode; the ANGLE branch stands down entirely and Avalonia drives rendering.
- **Secondary signal** — the Skia lease's `GRContext.Backend` is read once: `Metal` means ANGLE takes over, anything else hands control back to `OpenGlControlBase`.

The decision is logged as `[aura3d-angle] compositor backend=..., path=...` — check that line first when debugging.

## iOS: where the ANGLE frameworks come from

`Aura3D.Avalonia` resolves ANGLE's EGL/GLES2 symbols through `DllImport("__Internal")` (to avoid clashing with Apple's own OpenGLES symbols), so the two frameworks have to be linked into the **application** executable rather than living inside the class library.

**That part is now automatic.** `Aura3D.Avalonia`'s iOS target depends on `Aura3D.Angle.iOS`, and the package's `buildTransitive` targets pick the slice by `$(RuntimeIdentifier)` and inject the `NativeReference` items into the app project — an app needs zero ANGLE configuration. The dependency is pinned to an exact range (`[0.1.0]`): the slice and the P/Invoke signatures are an ABI pair, so a consumer must never be silently upgraded onto a slice that was not tested against them. Replacing the slices therefore means bumping that version and releasing `Aura3D.Avalonia` again.

```shell
dotnet add package Aura3D.Avalonia   # pulls Aura3D.Angle.iOS in on iOS
```

The package ships two ANGLE slices, `iossimulator-arm64` and `ios-arm64`, and has its own publishing channel: trigger `build-ios-lib.yml` manually and type the version into the form (the job packs with `-p:Version=<input>`, overriding the literal `<Version>` in the project, so a release needs no repository edit), verifies the version and both slices are inside the nupkg, and pushes to nuget.org through GitHub OIDC. `pack.yml` no longer publishes it and its artifact set no longer contains it (the browser-side `Aura3D.Avalonia.Browser` works the same way through `build-browser-lib.yml`). A missing slice fails the build loudly instead of quietly producing a blank viewport, and a version nuget.org already serves fails the publish job. The version now lives in two places that no longer reference each other: `<Version>` in `Aura3D.Angle.iOS.csproj` is what an in-repo build produces, and `Aura3DAngleIosVersion` in `Directory.Packages.props` only feeds the main package's exact range. So a slice hotfix runs: publish the slice with the new version → update both places to it (leave them disagreeing and the slice in this repository is no longer the one consumers resolve from nuget.org, while any later pack without `-p` produces the wrong number — the workflow flags that as an error; forget only the props side and the main package keeps pinning the old slice, which is a warning) → run `pack.yml` to re-publish the main package. Running `pack.yml` first is wrong too, and it now fails on its own: it restores that exact range from nuget.org, so pinning a version that was never published turns the restore red instead of shipping a package whose dependency nobody can resolve.

**Manual route** (when you build ANGLE yourself):

1. Build ANGLE for iOS from a standalone ANGLE checkout (not a Chromium checkout) with the Metal backend enabled; the only required gn argument is `enable_rust=false`. Drop the resulting `libEGL.framework` and `libGLESv2.framework` into `src/Aura3D.Angle.iOS/native/iossimulator-arm64/` and `native/ios-arm64/` (both slices are committed to the repository and packed as-is). `src/Aura3D.Angle.iOS/build-angle-ios.sh --device` codifies this; the device slice additionally needs `ios_enable_code_signing = false`, otherwise `gn gen` fails while looking for an "Apple Development" identity — which is exactly the situation in a certificate-free CI.
2. Adding `<PackageReference Include="Aura3D.Angle.iOS" />` to your own app works just as well as getting it transitively from `Aura3D.Avalonia`.
3. Do not guard such an `ItemGroup` with an `Exists(...)` condition: when the frameworks are missing the group is skipped silently, the app still compiles, and the viewport stays blank because the ANGLE session fails. Verify the frameworks really were linked — check that they appear under `<App>.app/Frameworks/`.
4. Hardware requirements: ANGLE's Metal backend needs Metal GPU family 4 (A11 or later); tvOS is not supported. The committed slices are built with ANGLE's default `ios_deployment_target`, i.e. `minos` 18.0, so an app deployment target below that produces a linker version mismatch warning.

## Browser: why the compositor's WebGL2 context is borrowed

Avalonia.Browser's `WebGlContext` is a singleton: `CanCreateSharedContext` is `false` and it exposes no GPU interop beyond `ISkiaSharpApiLeaseFeature`. So `OpenGlControlBase` can never initialize on wasm — the same silent failure as iOS under the Metal compositor — while a second WebGL context of our own could not share resources with the one Skia composites from.

The browser branch therefore **does not create a context**. A custom draw operation (`ICustomDrawOperation`) already runs on the compositor's render thread with Skia's WebGL2 context current, so the session allocates its output texture and FBO in that context, the pipeline renders into it, and the texture is imported zero-copy through the lease's `GRContext` via `GRGlTextureInfo`. The engine's GLES 3.0 calls do not go through Avalonia's GL bindings either: `DllImport("libSkiaSharp")` resolves emscripten's `eglGetProcAddress`, which returns real function pointers inside the application's wasm module (called as `delegate* unmanaged`, which is what Silk.NET's `GL.GetApi` expects), and emscripten's GLES→WebGL2 shim translates them.

Ownership is decided once, exactly like iOS, with the `[aura3d-webgl]` log prefix:

- **Authoritative signal** — `OpenGlControlBase.OnOpenGlInit` is called (cannot happen today; reserved as the fallback for whenever Avalonia supports shared contexts).
- **Secondary signal** — the Skia lease's `GRContext.Backend` is read once: `OpenGL` means the WebGL2 branch takes over, anything else hands control back to `OpenGlControlBase`.

## Browser: where the wasm link switches come from

The GLES entry points have to come out of the **application's own** wasm module. Without native linking the module simply has no `libSkiaSharp` symbols, and the app dies at startup with `System.DllNotFoundException: libSkiaSharp` (thrown from `SKImageInfo`'s static constructor, with no context to go on). Three things must therefore hold in the app project: `WasmBuildNative=true`, `-s FULL_ES3=1`, and `-s MIN/MAX_WEBGL_VERSION=2` (GLES3 entry points — VAO, UBO, 3D textures, blit — only exist on a WebGL2 context).

**Those three link switches are automatic.** `Aura3D.Avalonia`'s browser target depends on `Aura3D.Avalonia.Browser`, whose `buildTransitive` props inject them for `*-browser` targets only, so an application does not hand-write emscripten arguments. This mirrors the iOS slice package, including the exact `[0.1.0]` pin: the switches are paired with the library's GLES call surface, so they bump together with a browser verification run. This does not include the .NET 10 publish-time runtime settings below; the application project must still declare those three properties explicitly.

`WasmBuildNative=true` is however only a **necessary** condition: if the local SDK has no wasm-tools/emsdk workload, linking still does not happen and the build still succeeds. The SDK's own warning reads "neither $(WasmBuildNative), nor $(RunAOTCompilation) are 'true'" — a hardcoded string, misleading here because `WasmBuildNative` is in fact true (measured: `dotnet.native.wasm` is 3.0 MB, versus 25.6 MB once native is linked). The package's `buildTransitive` targets therefore raise an error when `RuntimeIdentifier=browser-wasm` and `WasmNativeWorkloadAvailable!=true`, naming `dotnet workload install wasm-tools`; projects that do not need this backend can set `Aura3DSkipWasmWorkloadCheck=true`. Same stance as on iOS: error out rather than silently render nothing.

To check the wiring landed: `dotnet msbuild <App>.Browser.csproj -getProperty:EmccExtraLDFlags -getProperty:WasmBuildNative -getProperty:WasmNativeWorkloadAvailable` (the last one is the direct verdict on the workload), and confirm the linked `dotnet.native.wasm` exports GLES3 symbols such as `glGenVertexArrays` — that is the shim being present.

<a id="browser-net10-release-config"></a>

### Required settings for .NET 10 Release/static publishing

With the current .NET 10 WebAssembly toolchain, every Aura3D `net10.0-browser` application must explicitly add
the following settings to the application `.csproj`. They form one required combination; do not enable only a subset:

```xml
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <PublishTrimmed>false</PublishTrimmed>
  <RunAOTCompilation>false</RunAOTCompilation>
  <WasmLinkIcalls>false</WasmLinkIcalls>
</PropertyGroup>
```

- Release full trimming removes declarations which incidentally supply some WASM interpreter-to-native trampolines.
  When Silk.NET first calls `gl.ClearColor(float, float, float, float)`, the `VFFFF` signature is missing. The log
  reaches `[aura3d-webgl] output ...`, then terminates with `aot-runtime-wasm.c:188 <disabled>` and
  `Program terminated with exit(1)`. The `aot-runtime-wasm.c` filename does not mean that the app enabled AOT.
- Disabling trimming alone restores the trampolines, but the .NET 10 linked-icall table can still disagree with the
  metadata tokens in the loaded `System.Private.CoreLib`. Startup then reports
  `Your mono runtime and class libraries are out of sync` / `function signature mismatch`.
  `WasmLinkIcalls=false` is therefore required as well.
- Enabling AOT alone, using `TrimMode=partial`, or rooting only `Silk.NET.OpenGLES` does not fix both failures and
  is not a substitute for this configuration.

After changing these properties, use fresh `bin`, `obj` and publish directories. An isolated artifacts root is the
recommended workflow:

```powershell
dotnet publish .\gallery\Aura3D.Gallery.Browser\Aura3D.Gallery.Browser.csproj `
  -c Release `
  --no-incremental `
  -p:UseArtifactsOutput=true `
  -p:ArtifactsPath="$PWD\artifacts\browser-release-clean"
```

The site is written to `artifacts/browser-release-clean/publish/Aura3D.Gallery.Browser/release/wwwroot`. Deploy it by
replacing the remote static-site directory as a whole; do not overlay it onto an old `_framework` directory. Also
invalidate `dotnet.js` in browser, Service Worker and CDN caches. Otherwise an old native wasm/CoreLib can be loaded
with a new manifest and produce the same CoreLib/icall failure.

`WEBGL_debug_renderer_info not enabled` / `INVALID_ENUM` is a harmless warning emitted while emscripten queries an
anonymous renderer; it is unrelated to either crash above.

## The GLES 3.0 subset: constraints for custom passes

Shaders and GL calls are written against a plain OpenGL ES 3.0 subset so that one pipeline works on desktop GL, Android GLES, ANGLE on iOS and WebGL2 in the browser. Some of these constraints bite for real under ANGLE:

- ANGLE's Metal backend only exposes ES 3.0 — requesting an ES 3.1/3.2 context fails with `EGL_BAD_MATCH` — and it does not provide `GL_EXT_float_blend`. **Drawing with blending enabled into a 32-bit float color attachment is treated as `GL_INVALID_OPERATION` and the entire draw is dropped silently**; the symptom is a black image, not an error. The framework's HDR render targets are therefore all `Rgba16f`; do not allocate an `Rgba32f` color attachment and then enable blending in a custom pass.
- Depth attachments `DEPTH_COMPONENT16/24/32F` and `DEPTH24_STENCIL8`/`DEPTH32F_STENCIL8` are ES 3.0 core and work fine under ANGLE — the restriction above concerns color attachments only.
- No compute. 3D textures cannot be imported as external textures.
- ANGLE translates GLSL to MSL and compiles it at runtime; there is no on-disk binary cache. Measured first-frame cost on iOS (iPhone 17 simulator / iOS 27.0): roughly 5.6–5.9 s for a PBR scene with cascaded shadows, 0.4–0.7 s for simple scenes.

WebGL2 validates more strictly than desktop GL and ANGLE. The following are "fine" elsewhere but are treated as `GL_INVALID_OPERATION` on WebGL2, which **drops the whole draw silently**:

- **Every enabled draw buffer must map to a fragment output that is actually written.** Two ways to trip over this: (1) a framebuffer with only a depth attachment still has `GL_DRAW_BUFFER0` defaulting to `COLOR_ATTACHMENT0`, while the shadow fragment shader has no color output — a depth-only FBO must call `glDrawBuffers([GL_NONE])` explicitly (`RenderTarget`, `CubeRenderTarget` and the CSM path all do); (2) a fragment shader declares `out` but no `#ifdef` branch ever writes it, so the compiled program has zero fragment outputs.
- **Shader-variant macros must be selected before the program is bound.** `UseShader(defines…)` only records the macros; `UseShader_Internal()` is what compiles and calls `glUseProgram`. Writing the two in the opposite order binds the previous frame's variant (on frame 1, the empty-macro one). The engine papered over this as "one frame late but stable" — the real cost was an empty first frame plus a WebGL2 error. New passes always go `UseShader` → `UseShader_Internal` → set uniforms → draw.
- **RGB-family internal formats are not color-renderable.** WebGL2 does not list `RGB8` / `RGB16F` / `RGB32F` as color-renderable; attaching one yields `FRAMEBUFFER_INCOMPLETE_ATTACHMENT`. Render targets must use the RGBA family (the irradiance map and the prefiltered environment map moved from `Rgb16f` to `Rgba16f`).
- **There is no `glGetTexLevelParameteriv`.** When importing a GL texture into Skia the internal format has to be stated explicitly in `GRGlTextureInfo`, otherwise `SKImage.FromTexture` returns `null` (Skia queries the format on desktop GL; WebGL2 exposes no entry point for it). Separately, querying the unmasked renderer while `WEBGL_debug_renderer_info` is disabled emits an `INVALID_ENUM` warning from emscripten itself — harmless.

## Browser performance and known gaps

- Debug and the currently verified Release configuration both run on the Mono interpreter, and first-frame cost for heavy scenes can be measured in minutes (a cascaded-shadow scene plus the HDR-to-cubemap passes took over 10 minutes to reach frame 1 in headless Chromium). This is host-side execution speed, not the render path. Do not enable `RunAOTCompilation` by itself as a performance workaround today: AOT does not restore a missing trampoline and still crashes on the first frame. Release performance work must wait for an upstream fix or for the Browser package to explicitly preserve every call signature required by Silk.NET.
- While an automated page stays `hidden`, `requestAnimationFrame` and `ResizeObserver` never fire at all, so neither Avalonia's render loop nor the canvas size moves. Verifying locally in a headless browser needs a temporary rAF/ResizeObserver shim — such shims **must not** go into `wwwroot`.
- Model import depends on the Assimp native library, and how that links in the browser is not verified yet. Model import on the other platforms is unaffected.

## Frame scheduling and thread semantics

- `RequestNextFrameRendering()` keeps the same signature on both backends. With `AutoRequestNextFrameRendering = false` the application requests each frame itself.
- On iOS the frame callback runs on the compositor render thread, unlike desktop where the callback is already on the UI thread. `SceneInitialized` / `SceneUpdated` / `ContextLost` / `ContextRestored` are marshalled back to the UI thread, so handlers can touch the control safely. The browser branch is structured the same way, and its context is thread-affine to the render thread — when the control detaches, GL calls cannot be issued from the UI thread; teardown has to be posted as a `Compositor.RequestCompositionUpdate` task so it runs there.
- Detaching the control from the visual tree destroys the context and actually deletes GL objects, returning VRAM. Scene, nodes and CPU-side resources are preserved, and re-attaching raises `ContextRestored` rather than `SceneInitialized` again. This matches desktop `OnOpenGlDeinit` semantics; see [GPU Resource Lifecycle](./gpu-resource-lifecycle.md) for details.

## Known issues on the GL (EAGL) fallback path

This path is only used when the host explicitly sets `iOSRenderingMode.OpenGl` (Avalonia `OpenGlControlBase` on Apple EAGL). Default iOS configurations never reach it. Current knowledge comes from simulator runs and has not been verified on a device:

- Scenes using the PBR deferred pipeline take minutes per frame — the simulator's EAGL is a translated/software path and cannot carry several large float render targets plus cascaded shadows. On-device EAGL is hardware-driven and may behave differently.
- Under cascaded shadow maps the ground plane stops producing fragments from the third frame on, while a minimal program that only consumes `gl_VertexID` still lands in the same frame — which points at a silently dropped draw inside the simulator's GLES-on-Metal layer.

Bottom line: stay on the default (ANGLE) path on iOS; there is no reason to force OpenGL mode anymore.
