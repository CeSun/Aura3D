# Aura3D.Avalonia.Browser

[Aura3D](https://github.com/CeSun/Aura3d) 的浏览器渲染后端借 Avalonia.Browser 合成器的 WebGL2 上下文出图：
引擎按 OpenGL ES 3.0 发调用，经 emscripten 的 GLES shim 落到 WebGL2。这条链路要求**应用**自己的
wasm 模块带着那份 shim 一起链接，而链接开关只有应用工程说得上话，所以由本包在装包时注入。

## 用法

不需要单独装。`Aura3D.Avalonia` 的 browser 目标以精确区间依赖本包，包内 `buildTransitive` props
会穿透到应用工程生效，所以应用侧一行 wasm 配置都不用写：

```shell
dotnet add package Aura3D.Avalonia
```

注入的内容（仅对 `$(TargetFramework)` 含 `-browser` 的工程生效）：

- `WasmBuildNative=true` —— 原生库随应用 wasm 模块链接，GLES 入口点与 Avalonia 的 WebGL 渲染目标都取它；
- `-s FULL_ES3=1` —— 把 GLES2/GLES3 实现链进模块（默认只有 WebGL1 通道）；
- `-s MIN_WEBGL_VERSION=2 -s MAX_WEBGL_VERSION=2` —— GLES3 的入口点（VAO、UBO、3D 纹理、blit）
  只在 WebGL2 上下文上存在，上下文版本必须是 2。

应用本身仍按 .NET WASM 的常规方式跑：`dotnet run --project <App>.Browser`。

## 版本策略

包里没有二进制，只有构建属性，但 `Aura3D.Avalonia` 仍以精确区间 `[0.1.0]` 依赖它：这些开关与
库的 GLES 调用面是配对的（比如 `FULL_ES3` 少了就整块不出图），升级要和库一起过一遍浏览器验证。
