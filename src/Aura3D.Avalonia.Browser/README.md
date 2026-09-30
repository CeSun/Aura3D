# Aura3D.Avalonia.Browser

English | [中文](./README_CN.md)

[Aura3D](https://github.com/CeSun/Aura3d) renders in the browser through the WebGL2 context of the
Avalonia.Browser compositor: the engine issues OpenGL ES 3.0 calls that land on WebGL2 via emscripten's
GLES shim. That chain requires the **application's** own wasm module to link that shim in, and only the
application project can decide the linker switches — so this package injects them when it is installed.

## Usage

There is nothing to install separately. The browser target of `Aura3D.Avalonia` depends on this package
with an exact version range, and the `buildTransitive` props inside the package flow through to the
application project, injecting the native linking and the WebGL2 switches automatically:

```shell
dotnet add package Aura3D.Avalonia
```

What gets injected (applies only to projects whose `$(TargetFramework)` contains `-browser`):

- `WasmBuildNative=true` — native libraries are linked into the application wasm module; both the GLES
  entry points and Avalonia's WebGL render target take them from there;
- `-s FULL_ES3=1` — links the GLES2/GLES3 implementation into the module (by default only the WebGL1
  path is present);
- `-s MIN_WEBGL_VERSION=2 -s MAX_WEBGL_VERSION=2` — the GLES3 entry points (VAO, UBO, 3D textures, blit)
  only exist on a WebGL2 context, so the context version must be 2.

The application itself still runs the regular .NET WASM way: `dotnet run --project <App>.Browser`.

## .NET 10 publish / static site: required in the app project

> [!IMPORTANT]
> With the current .NET 10 WebAssembly toolchain, "it runs with `dotnet run`" does **not** mean a Release
> static publish will run. Every `net10.0-browser` application that uses Aura3D **must** explicitly write
> the following three properties in the application `.csproj`; setting only one or two of them still
> crashes at startup or on the first rendered frame.

```xml
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <PublishTrimmed>false</PublishTrimmed>
  <RunAOTCompilation>false</RunAOTCompilation>
  <WasmLinkIcalls>false</WasmLinkIcalls>
</PropertyGroup>
```

The three must be used as a single set:

- The default Release full trimming drops the WASM interpreter-to-native trampolines that Silk.NET
  function-pointer calls need. On the first execution of calls such as
  `gl.ClearColor(float, float, float, float)` the typical log is `aot-runtime-wasm.c:188 <disabled>`,
  followed by `Program terminated with exit(1)`.
- Setting only `PublishTrimmed=false` restores those trampolines, but .NET 10's reduced icall table can
  still be out of sync with the metadata tokens of the actually loaded `System.Private.CoreLib`, which
  reports `Your mono runtime and class libraries are out of sync` / `function signature mismatch` during
  startup.
- `WasmLinkIcalls=false` avoids that second problem; the currently verified publish path uses the
  interpreter, so `RunAOTCompilation` must stay `false`. Enabling AOT alone, switching to
  `TrimMode=partial`, or rooting only `Silk.NET.OpenGLES` is not a substitute for this set.

After switching configuration you must also use a clean intermediate and publish directory. For example:

```powershell
dotnet publish .\gallery\Aura3D.Gallery.Browser\Aura3D.Gallery.Browser.csproj `
  -c Release `
  --no-incremental `
  -p:UseArtifactsOutput=true `
  -p:ArtifactsPath="$PWD\artifacts\browser-release-clean"
```

The output lands in `artifacts/browser-release-clean/publish/Aura3D.Gallery.Browser/release/wwwroot`. When
deploying, **replace** that `wwwroot` as a whole into an empty site directory — do not copy it on top of
an existing one. Otherwise several generations of `dotnet.native.*.wasm`,
`System.Private.CoreLib.*.wasm` and the new `dotnet.js` end up mixed together, which can still trigger
the CoreLib/icall mismatch. After deploying, also refresh the `dotnet.js` cache in the browser,
in Service Workers and in the CDN.

`WEBGL_debug_renderer_info not enabled` / `INVALID_ENUM` in the console are harmless renderer-info
warnings, not the cause of the crashes above.

## Prerequisite: the wasm-tools workload

`WasmBuildNative=true` is only a necessary condition — the local SDK must also have `wasm-tools`
(which ships emscripten) installed, otherwise native linking never happens: the build reports 0 errors,
`dotnet.native.wasm` stays just over 3 MB (about 25 MB once linked), and the app dies at startup with
`System.DllNotFoundException: libSkiaSharp`. In that case the `buildTransitive` targets in this package
raise a build-time Error with the command to run:

```shell
dotnet workload install wasm-tools
```

Projects that genuinely do not need this backend (for example desktop-only) can set
`Aura3DSkipWasmWorkloadCheck=true` to turn the check off.

## Versioning policy

The package carries no binaries, only build properties, yet `Aura3D.Avalonia` still depends on it with
the exact range `[0.1.0]`: these switches are paired with the GLES call surface of the library (drop
`FULL_ES3`, for instance, and nothing renders at all), so any upgrade has to go through browser
verification together with the library.
