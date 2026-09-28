# Aura3D.Avalonia.Browser

[Aura3D](https://github.com/CeSun/Aura3d) 的浏览器渲染后端借 Avalonia.Browser 合成器的 WebGL2 上下文出图：
引擎按 OpenGL ES 3.0 发调用，经 emscripten 的 GLES shim 落到 WebGL2。这条链路要求**应用**自己的
wasm 模块带着那份 shim 一起链接，而链接开关只有应用工程说得上话，所以由本包在装包时注入。

## 用法

不需要单独装。`Aura3D.Avalonia` 的 browser 目标以精确区间依赖本包，包内 `buildTransitive` props
会穿透到应用工程，自动注入原生链接和 WebGL2 开关：

```shell
dotnet add package Aura3D.Avalonia
```

注入的内容（仅对 `$(TargetFramework)` 含 `-browser` 的工程生效）：

- `WasmBuildNative=true` —— 原生库随应用 wasm 模块链接，GLES 入口点与 Avalonia 的 WebGL 渲染目标都取它；
- `-s FULL_ES3=1` —— 把 GLES2/GLES3 实现链进模块（默认只有 WebGL1 通道）；
- `-s MIN_WEBGL_VERSION=2 -s MAX_WEBGL_VERSION=2` —— GLES3 的入口点（VAO、UBO、3D 纹理、blit）
  只在 WebGL2 上下文上存在，上下文版本必须是 2。

应用本身仍按 .NET WASM 的常规方式跑：`dotnet run --project <App>.Browser`。

## .NET 10 发布/静态站点：应用工程必填

> [!IMPORTANT]
> 当前 .NET 10 WebAssembly 工具链下，`dotnet run` 能运行不代表 Release 静态发布可以直接运行。
> 每个使用 Aura3D 的 `net10.0-browser` 应用都**必须**在应用 `.csproj` 中显式写入以下三项；
> 只写其中一项或两项仍会在启动或首帧渲染时崩溃。

```xml
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <PublishTrimmed>false</PublishTrimmed>
  <RunAOTCompilation>false</RunAOTCompilation>
  <WasmLinkIcalls>false</WasmLinkIcalls>
</PropertyGroup>
```

三项必须作为一个整体使用：

- 默认 Release full trimming 会漏掉 Silk.NET 函数指针调用需要的 WASM interpreter-to-native trampoline。
  首次执行 `gl.ClearColor(float, float, float, float)` 等调用时，常见日志是
  `aot-runtime-wasm.c:188 <disabled>`，随后 `Program terminated with exit(1)`。
- 只设置 `PublishTrimmed=false` 虽然会恢复这些 trampoline，但 .NET 10 的精简 icall 表仍可能与实际加载的
  `System.Private.CoreLib` metadata token 不一致，启动阶段会报
  `Your mono runtime and class libraries are out of sync` / `function signature mismatch`。
- `WasmLinkIcalls=false` 用于避开第二个问题；当前已验证的发布路径使用解释器，因此
  `RunAOTCompilation` 必须保持 `false`。单独开启 AOT、改成 `TrimMode=partial` 或只 root
  `Silk.NET.OpenGLES` 都不能替代这组配置。

切换配置后还必须使用全新的中间目录和发布目录。例如：

```powershell
dotnet publish .\example\Example.Browser\Example.Browser.csproj `
  -c Release `
  --no-incremental `
  -p:UseArtifactsOutput=true `
  -p:ArtifactsPath="$PWD\artifacts\browser-release-clean"
```

产物位于 `artifacts/browser-release-clean/publish/Example.Browser/release/wwwroot`。部署时应把这份
`wwwroot` **整体替换**到一个空的站点目录，不要覆盖追加到旧目录；否则多代
`dotnet.native.*.wasm`、`System.Private.CoreLib.*.wasm` 与新的 `dotnet.js` 混在一起，仍可能触发
CoreLib/icall 不同步。部署后还应刷新浏览器、Service Worker 和 CDN 中的 `dotnet.js` 缓存。

控制台里的 `WEBGL_debug_renderer_info not enabled` / `INVALID_ENUM` 是无害的 renderer 信息查询警告，
不是上述崩溃的根因。

## 前置条件：wasm-tools workload

`WasmBuildNative=true` 只是必要条件——本机 SDK 还得装 `wasm-tools`（含 emscripten），否则原生链接不会发生：
构建 0 错误、`dotnet.native.wasm` 只有 3 MB 出头（链上后约 25 MB），应用启动即
`System.DllNotFoundException: libSkiaSharp`。这种情况由包内 `buildTransitive` targets 在构建期直接报 Error
并给出命令：

```shell
dotnet workload install wasm-tools
```

确实不需要本后端的工程（比如只在桌面上跑）可设 `Aura3DSkipWasmWorkloadCheck=true` 关掉这条检查。

## 版本策略

包里没有二进制，只有构建属性，但 `Aura3D.Avalonia` 仍以精确区间 `[0.1.0]` 依赖它：这些开关与
库的 GLES 调用面是配对的（比如 `FULL_ES3` 少了就整块不出图），升级要和库一起过一遍浏览器验证。
