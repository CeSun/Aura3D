---
section: advanced
order: 4
---

# 自定义渲染管线

这一篇面向**扩展引擎**的人：自己搭管线、写自己的 `RenderPass`。需要基本渲染知识（着色器、Uniform、帧缓冲）；VAO/VBO 已由引擎封装。只想选内置管线见 [选择与配置管线](./pipelines.md)。

## 两个角色

自定义管线由两个基类撑起，各司其职：

- **`RenderPipeline`**：装配工。注册渲染目标（帧缓冲）和渲染步骤（Pass），决定这些步骤按什么顺序、对每个相机跑几次，并在每帧的固定节点上回调钩子。
- **`RenderPass`**：干活的。一段由着色器驱动的绘制流程——绑输出、设 Uniform、画网格或画全屏四边形。通常一个 Shader（含它的宏变体）对应一个 Pass。

```
RenderPipeline（继承它，在构造函数里装配）
  ├── RegisterRenderTarget：注册帧缓冲 + 颜色/深度纹理附件
  ├── RegisterRenderPass：注册渲染步骤，指定输出目标与执行时机
  └── 调度执行
       ├── RenderPassGroup.Once — 全局执行一次（如 ShadowMap）
       └── RenderPassGroup.EveryCamera — 每个相机各执行一次（如主渲染）
```

## 装配一条管线：RenderPipeline

管线在自己的构造函数里把目标、Pass 一一登记。下面是内置 NoLight 管线的真实装配过程（[NoLightPipeline.cs](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Renderers/NoLight/NoLightPipeline.cs)）：

```csharp
public class NoLightPipeline : RenderPipeline, IRenderPipelineCreateInstance
{
    public NoLightPipeline(Scene scene) : base(scene)
    {
        // 1. 注册渲染目标（帧缓冲 + 附件）
        var baseRenderTarget = RegisterRenderTarget("BaseRenderTarget")
            .AddTexture("Color", TextureFormat.Rgba16f)
            .SetDepthTexture(Settings.DepthFormat);

        var gammaOutput = RegisterRenderTarget("GammaOutput")
            .AddTexture("Color", TextureFormat.Rgba8)
            .SetDepthTexture(Settings.DepthFormat);

        var noLightPass = new NoLightPass(this);

        // 2. 按顺序注册 Pass，每个都用 SetOutput 指定写到哪个目标
        RegisterRenderPass(new BackgroundPass(this).SetOutput(baseRenderTarget),
            RenderPassGroup.EveryCamera);
        RegisterRenderPass(noLightPass.SetOutput(baseRenderTarget),
            RenderPassGroup.EveryCamera);
        RegisterRenderPass(new ParticlePass(this).SetOutput(baseRenderTarget),
            RenderPassGroup.EveryCamera);
        RegisterRenderPass(
            new GammaCorrectionPass(this, baseRenderTarget.GetTexture("Color"))
                .SetOutput(gammaOutput),
            RenderPassGroup.EveryCamera);
        // 最后一个 Pass 输出到 CameraOutput，即屏幕
        RegisterRenderPass(
            new FxaaPass(this, gammaOutput.GetTexture("Color")).SetOutput(CameraOutput),
            RenderPassGroup.EveryCamera);

        RegisterDebugPass(baseRenderTarget);
    }

    public static RenderPipeline CreateInstance(Scene scene) => new NoLightPipeline(scene);
}
```

**关键 API：**

| 方法 | 说明 |
|---|---|
| `RegisterRenderTarget(name)` | 注册一个帧缓冲，返回配置器 |
| `.AddTexture(name, format)` | 给该目标加一个颜色附件 |
| `.SetDepthTexture(format)` | 给该目标加一个深度附件（常用 `Settings.DepthFormat`） |
| `RegisterRenderPass(pass, group)` | 注册渲染步骤；`group` 决定执行时机，Pass 按注册顺序执行 |
| `pass.SetOutput(target)` | 指定这个 Pass 画到哪里；不指定时默认 `CameraOutput`（屏幕） |
| `CameraOutput` | 代表"当前相机的最终输出"的输出引用，通常给链路上最后一个 Pass |

**`RenderPassGroup` 两个取值：**

- `EveryCamera` — 每个相机各执行一次（主渲染链路的绝大多数 Pass）。
- `Once` — 每帧全局执行一次（在遍历相机之前先跑，如 ShadowMap 预渲染）。

> [!NOTE]
> 管线登记后要在 `CreateRenderPipeline` 里交给控件：`view.CreateRenderPipeline = scene => new NoLightPipeline(scene);`，且必须赶在 GL 初始化前（见 [选择与配置管线](./pipelines.md)）。想被控件按名字自动实例化，实现 `IRenderPipelineCreateInstance` 并提供静态 `CreateInstance` 即可。

## 写一个 RenderPass：最小实现

一个 Pass 就是：告诉它用哪份着色器源码，重写 `Render(Camera)` 画东西。下面是 NoLight 风格 Pass 的完整写法：

```csharp
public class NoLightPass : RenderPass
{
    public NoLightPass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
        // 指定着色器源码
        this.FragmentShader = ShaderResource.NoLightFrag;
        this.VertexShader = ShaderResource.NoLightVert;
    }

    public override void Render(Camera camera)
    {
        // 渲染不透明非骨骼网格
        UseShader();
        RenderVisibleMeshesInCamera(
            mesh => !mesh.IsSkinnedMesh
                 && (mesh.Material == null
                     || mesh.Material.BlendMode == BlendMode.Opaque),
            camera.View, camera.Projection);

        // 渲染不透明骨骼网格（用 SKINNED_MESH 宏变体）
        UseShader("SKINNED_MESH");
        RenderVisibleMeshesInCamera(
            mesh => mesh.IsSkinnedMesh
                 && (mesh.Material == null
                     || mesh.Material.BlendMode == BlendMode.Opaque),
            camera.View, camera.Projection);
    }
}
```

> 新写管线时**优先用带剔除的渲染方法**（`RenderVisibleMeshesInCamera`），它只遍历当前相机的可见网格，性能最好。`mesh.IsStaticMesh` / `mesh.IsSkinnedMesh` 是 `Mesh` 上的属性，用来区分静态/骨骼网格，省去手动判断骨架。

### 网格筛选与渲染方法速查

**首选——剔除后渲染：**

```csharp
// 渲染通过视锥体剔除的网格（Mesh）
RenderVisibleMeshesInCamera(filter, camera.View, camera.Projection);

// 渲染通过视锥体剔除的实例化网格（InstancedMesh）
RenderVisibleInstancedMeshesInCamera(filter, camera.View, camera.Projection);
```

典型的不透明 Pass：

```csharp
public override void Render(Camera camera)
{
    // 不透明静态网格
    UseShader();
    RenderVisibleMeshesInCamera(
        mesh => mesh.IsStaticMesh
             && (mesh.Material == null || mesh.Material.BlendMode == BlendMode.Opaque),
        camera.View, camera.Projection);

    // 不透明骨骼网格
    UseShader("SKINNED_MESH");
    RenderVisibleMeshesInCamera(
        mesh => mesh.IsSkinnedMesh
             && (mesh.Material == null || mesh.Material.BlendMode == BlendMode.Opaque),
        camera.View, camera.Projection);

    // 实例化网格
    RenderVisibleInstancedMeshesInCamera(
        im => im.EnableFrustumCulling, camera.View, camera.Projection);
}
```

**备选——全量渲染**（仅在物体极少、剔除开销大于收益；需要按类型遍历；从外部预筛列表；调试临时关剔除时）：

```csharp
RenderMeshes(filter, view, proj);              // 全部 Mesh，不看可见性
RenderStaticMeshes(filter, view, proj);        // 仅静态 Mesh
RenderSkinnedMeshes(filter, view, proj);       // 仅骨骼 Mesh
RenderInstancedMeshes(filter, view, proj);     // 全部实例化网格
RenderMeshesFromList(list, filter, view, proj);// 从指定列表渲染
```

| 方法 | 类型 | 剔除 | 推荐度 |
|---|---|---|---|
| `RenderVisibleMeshesInCamera(filter, view, proj)` | Mesh | ✅ | ⭐ 首选 |
| `RenderVisibleInstancedMeshesInCamera(filter, view, proj)` | InstancedMesh | ✅ | ⭐ 首选 |
| `RenderMeshesFromList(list, filter, view, proj)` | Mesh | ❌ | 外部列表场景 |
| `RenderStaticMeshes(filter, view, proj)` | Mesh | ❌ | 按类型遍历 |
| `RenderSkinnedMeshes(filter, view, proj)` | Mesh | ❌ | 按类型遍历 |
| `RenderMeshes(filter, view, proj)` | Mesh | ❌ | 调试 / 少量物体 |
| `RenderInstancedMeshes(filter, view, proj)` | InstancedMesh | ❌ | 调试 / 少量物体 |

> [!TIP]
> `RenderVisibleMeshesInCamera` 依赖管线在本相机渲染前算好的可见列表，而这份列表又受 `PipelineSettings.EnableFrustumCulling` 开关影响（见 [选择与配置管线](./pipelines.md#视锥体剔除)）。剔除关掉时，可见方法会退化成遍历全部网格。

## 给单个 Mesh 传参

要按网格下不同的参数（颜色、开关等），重写 `RenderMesh`，在渲染每个网格前设好它的 Uniform。注意 `base.RenderMesh` 已经会帮你设 `modelMatrix`、绑材质参数并发起绘制，所以你只需补自己那份 Uniform，并**务必设置 view / projection**：

```csharp
public override void RenderMesh(Mesh mesh, Matrix4x4 view, Matrix4x4 projection)
{
    if (someCondition)
    {
        UniformFloat("someParameter", value);
        UniformVector4("someColor", new Vector4(1, 0, 0, 1));
    }

    // 这两个基础矩阵必须设置
    UniformMatrix4("viewMatrix", view);
    UniformMatrix4("projectionMatrix", projection);

    base.RenderMesh(mesh, view, projection);
}
```

**常用 Uniform 方法：**

| 方法 | 用途 |
|---|---|
| `UniformInt(name, value)` | int |
| `UniformFloat(name, value)` | float |
| `UniformVector2 / 3 / 4(name, value)` | 向量 |
| `UniformMatrix4(name, value)` | 4×4 矩阵 |
| `UniformMatrix4Array(name, span)` | 矩阵数组 |
| `UniformColor(name, color)` | 颜色（内部转 vec3） |
| `UniformTexture(name, texture)` | 2D 纹理（自动占一个纹理单元） |
| `UniformTextureCubeMap(name, texture)` | 立方体贴图 |

> 若 `CurrentShader` 还没激活，或着色器里根本没有这个 Uniform 名，`Uniform*` 会静默跳过、不会报错——这也是下面"UseShader 必须在前"这条坑常见的表现。

## 着色器宏系统

管线的着色器"变体"（同一份源码按不同宏编译出的多份程序）靠三个方法协作。理解它们的关系是自定义管线的核心。

### 三个方法的分工

| 方法 | 作用 | 是否碰 GPU |
|---|---|---|
| `UseShader(params string[] defines)` | **替换**当前 defines 列表 | 否，只记意图 |
| `AddDefines(params string[] defines)` | **追加**到当前 defines 列表 | 否 |
| `UseShader_Internal(...)` | 读当前 defines，编译/缓存/激活对应变体 | 是，`gl.UseProgram` |

`UseShader` / `AddDefines` 是**声明式**的——只记录"我想要哪些宏"，不碰 GPU。真正的编译与绑定发生在 `UseShader_Internal`，它由 `RenderVisibleMeshesInCamera` 等渲染方法在每个 Mesh 绘制前**自动调用**。

```
1. UseShader("SKINNED_MESH")     → defines = ["SKINNED_MESH"]
2. RenderVisibleMeshesInCamera(...)
   ├─ 每个 mesh：
   │   UseShader_Internal(mesh)  → 读到 defines = ["SKINNED_MESH"]
   │      缓存 key = "SKINNED_MESH"；命中直接 gl.UseProgram，未命中则编译 + 缓存
   │   RenderMesh(mesh, ...)     → 设 Uniform、gl.DrawElements
3. UseShader("SKINNED_MESH", "BLENDMODE_MASKED")
                                → defines = ["SKINNED_MESH", "BLENDMODE_MASKED"]
4. RenderVisibleMeshesInCamera(...)
   └─ UseShader_Internal(mesh)  → 缓存 key = "SKINNED_MESH;BLENDMODE_MASKED"（不同 key = 不同变体）
```

当一组网格共享大部分宏、只差个别时，用 `AddDefines` 追加而不是重复全列：

```csharp
UseShader("SKINNED_MESH");
RenderVisibleMeshesInCamera(filter1, camera.View, camera.Projection);

// 追加一个宏，编译出 SKINNED_MESH + BLENDMODE_MASKED 变体
AddDefines("BLENDMODE_MASKED");
RenderVisibleMeshesInCamera(filter2, camera.View, camera.Projection);
```

（还有一个 `RemoveDefines(params string[])` 从当前列表移除指定宏，用法同上。）

### 编译流程（压缩版）

`UseShader_Internal` 被调用时大致做这几步：

1. 把 `defines` 用 `;` 拼成缓存 key（如 `"SKINNED_MESH;BLENDMODE_MASKED"`）。
2. 若材质通过 `SetShaderSource` 提供了自定义源码 → 查材质级缓存，未命中就用材质源码编译；否则查 Pass 级缓存，未命中就用 Pass 的 `VertexShader`/`FragmentShader` 编译。
3. 编译时把 `#define SKINNED_MESH\n...` 注入到源码里的 `//{{defines}}` 位置。
4. 按当前上下文自动选方言（首次取用读 `GL_VERSION`）：GLES 上下文用原样源码；桌面 GL 上下文把 `#version 300 es` 换成 `#version 410 core` 并去掉 `precision` 声明（macOS 只有桌面 GL，走这条；桌面侧需 GL 4.1+）。
5. 链接程序、枚举所有 Uniform 位置并缓存。

### 两级缓存

同一份 defines 组合只编译一次，之后各帧复用缓存的程序句柄。缓存分两级：

| 缓存层 | 存储位置 | 什么时候用 |
|---|---|---|
| Pass 级 | `RenderPass.Shaders["key"]` | 材质没有自定义着色器时 |
| Material 级 | 材质 GPU 状态里，按 `ShaderName;key` | 材质通过 `SetShaderSource` 覆盖了着色器源码时 |

### 着色器源码里的宏注入点

GLSL 源码用 `//{{defines}}` 作为宏注入标记：

```glsl
#version 300 es
precision mediump float;

//{{defines}}   ← 编译时替换为 #define SKINNED_MESH 等

layout(location = 0) in vec3 position;

#ifdef INSTANCED_MESH
layout(location = 8) in mat4 modelMatrix;
#endif

#ifndef INSTANCED_MESH
uniform mat4 modelMatrix;
#endif
```

> [!IMPORTANT]
> **defines 的顺序影响缓存 key。** 它用 `;` 拼接：`UseShader("A").AddDefines("B")` 得到 `"A;B"`，`UseShader("A", "B")` 也是 `"A;B"`，一致；但先 `UseShader("B")` 再 `AddDefines("A")` 得到 `"B;A"`，是**另一个**变体。建议总是用一次 `UseShader` 把所有需要的宏一次性声明齐。

## 后处理 Pass：手动 UseShader_Internal + RenderQuad / RenderCube

上面那些渲染方法会自动帮你调 `UseShader_Internal`。但**如果某个 Pass 不遍历 Mesh**——比如后处理 Pass 只画一个全屏四边形——就得**手动调用**它。`RenderQuad()` 画一个覆盖 NDC 的四边形、`RenderCube()` 画一个单位立方体，两者都是 `RenderPass` 的内置方法，用于后处理和调试。

后处理 Pass 的标准流程：

```
UseShader()           → 声明宏（可选）
UseShader_Internal()  → 编译/激活对应变体（手动！）
UniformTexture(...)   → 设置输入纹理等 Uniform
RenderQuad()          → 绘制全屏四边形
```

真实例子——伽马校正 Pass（[GammaCorrectionPass.cs](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Renderers/Common/GammaCorrectionPass.cs)）：

```csharp
public override void Render(Camera camera)
{
    BindOutputRenderTarget(camera);
    var source = GetTexture(inputTexture, camera);

    gl.Disable(EnableCap.DepthTest);
    gl.Disable(EnableCap.Blend);

    UseShader();              // 无宏变体，可省略
    ClearTextureUnit();       // 清空纹理单元计数器
    UseShader_Internal();     // ← 手动激活！此处无 Material 上下文，传 null
    UniformTexture("colorTexture", source);
    RenderQuad();             // 画全屏四边形，采样输入纹理做伽马校正
}
```

FXAA Pass 同理（[FxaaPass.cs](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Renderers/Common/FxaaPass.cs)）：

```csharp
UseShader();
ClearTextureUnit();
UseShader_Internal();
UniformTexture("u_texture", rt.GetTexture(inputTextureName));
UniformVector2("u_textureSize", new Vector2(texWidth, texHeight));
RenderQuad();
```

带宏变体的后处理——PBR 的 IBL 环境光 Pass（[IBLAmbientPass.cs](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Pipeline.PBR/IBLAmbientPass.cs)）：

```csharp
UseShader("ENBALE_DEFERRED_SHADING");  // 先声明宏
UseShader_Internal();                  // 再编译带宏的变体并激活
ClearTextureUnit();
UniformTexture("gBufferBaseColor", gBufferBaseColor);
UniformTexture("gBufferNormalRoughness", gBufferNormalRoughness);
// ... 更多 Uniform ...
UniformMatrix4("u_viewMatrix", camera.View);
UniformMatrix4("u_projMatrix", camera.Projection);
RenderQuad();
```

> [!IMPORTANT]
> **`UseShader` / `AddDefines` 必须在 `UseShader_Internal` 之前调用。** `UseShader_Internal` 读取的是**当下**的 defines 列表来决定激活哪个变体；它执行完之后再改 defines，不会影响已经激活的那份着色器。手写后处理 Pass 忘了先 `UseShader()`，或把顺序反了，是最常见的空屏 / 变体错乱来源。

## 生命周期钩子与网格排序

`RenderPipeline` 和 `RenderPass` 都提供虚方法，让你在渲染流程的固定节点插入逻辑。

### RenderPipeline 钩子

```csharp
public class MyPipeline : RenderPipeline
{
    // GL 初始化完成后调用一次（在注册好 RenderTarget/RenderPass 之后）
    public override void Setup() { }

    // 整帧渲染前（每帧一次，在所有相机之前）
    public override void BeforeRender() { }

    // 整帧渲染后（每帧一次，在所有相机之后）
    public override void AfterRender() { }

    // 每个相机渲染前 / 后
    public override void BeforeCameraRender(Camera camera) { }
    public override void AfterCameraRender(Camera camera) { }

    // 自定义网格排序（默认按到相机距离排，可覆写，如透明物按远到近排）
    public override void SortMeshes(IReadOnlyList<Mesh> meshes, Camera camera)
    {
        base.SortMeshes(meshes, camera);
    }
}
```

内置 NoLight 管线就在 `BeforeCameraRender` 里对可见网格排序并设视口：

```csharp
public override void BeforeCameraRender(Camera camera)
{
    base.BeforeCameraRender(camera);
    if (gl == null) return;
    SortMeshes(VisibleMeshesInCamera, camera);
    gl.Viewport(0, 0, camera.Width, camera.Height);
}
```

### RenderPass 钩子

```csharp
public class MyPass : RenderPass
{
    // Pass 首次初始化时调用一次
    public override void Setup() { }

    // 每帧前 / 后（Once 类型的 Pass 走这组无参版本）
    public override void BeforeRender() { }
    public override void AfterRender() { }

    // 每个相机前 / 后（EveryCamera 类型的 Pass 走这组带 camera 版本）
    public override void BeforeRender(Camera camera) { }
    public override void AfterRender(Camera camera) { }
}
```

> 你真正的绘制写在 `Render()`（Once 类型）或 `Render(Camera camera)`（EveryCamera 类型）里，管线调度时会自动夹在对应的 Before/After 之间。

## 只想改某个材质，不改整条管线？

如果目标只是让某个特定材质用不同的着色器，而不是新建一套管线/Pass，那用材质级的着色器替换更合适——`material.SetShaderSource(passKey, ShaderType.Vertex/Fragment, src)`，材质参数用 `SetParameterValue` 设置，Pass 会按同名 uniform 自动绑定。这属于材质扩展，完整做法见 [自定义材质与着色器](./custom-material.md)。

## 常见坑

- **`UseShader` 排在 `UseShader_Internal` 之后**：手写后处理/自定义 Pass 时，先声明宏再激活；顺序反了，`UseShader_Internal` 读到的是旧 defines，画出来变体不对甚至空屏。
- **宏顺序改变缓存 key**：`"A;B"` 与 `"B;A"` 是两个变体。用一次 `UseShader` 声明全部宏，别拼凑出不同顺序。
- **忘调 `UseShader_Internal`**：不遍历网格的 Pass（纯 `RenderQuad` / `RenderCube`）必须手动激活着色器，否则 `CurrentShader` 为空，`Uniform*` 全被静默跳过。
- **重写 `RenderMesh` 没设 view / projection**：`base.RenderMesh` 只帮你设 `modelMatrix` 与材质参数，`viewMatrix`/`projectionMatrix` 得自己设，否则投影错乱。
- **Pass 没 `SetOutput`**：不指定输出时默认落 `CameraOutput`（屏幕）；中间 Pass 忘了设输出会把结果直接糊到屏幕上。
- **创建管线的时机**：自定义管线要靠 `view.CreateRenderPipeline = scene => new MyPipeline(scene);` 装载，且必须赶在 GL 初始化前。
- **着色器里没有对应宏注入点**：源码没写 `//{{defines}}`，`UseShader("SOME_MACRO")` 的 `#define` 就注入不进去，`#ifdef` 恒不成立。

## 可运行示例与源码

- NoLight 管线与 Pass（装配 + EveryCamera 链路）：[NoLightPipeline.cs](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Renderers/NoLight/NoLightPipeline.cs)
- 手动 `UseShader_Internal` 的后处理：[GammaCorrectionPass.cs](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Renderers/Common/GammaCorrectionPass.cs)、[FxaaPass.cs](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Renderers/Common/FxaaPass.cs)
- 带宏变体的后处理：[IBLAmbientPass.cs](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Pipeline.PBR/IBLAmbientPass.cs)
- 内置 Pass 与宏速查（含各 Pass 用哪些宏、注入点约定）：[内置 Pass 与着色器宏速查](./reference-shaders.md)

## 下一步

- 想按"用户要做的事"来选/配管线：[选择与配置管线](./pipelines.md)
- 材质与着色器扩展：[自定义材质与着色器](./custom-material.md)
