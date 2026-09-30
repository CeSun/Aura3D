---
section: notes
order: 4
---

# 内置 Pass 与着色器宏速查

**纯速查**：内置 Pass 清单、着色器宏、顶点属性 location 约定、引擎固定 uniform 与方言规则。写自定义 Pass 时当字典用。

## 内置 Pass 清单

### 通用 Pass（`Aura3D.Core`）

| Pass | 作用 | 调度组 | 所属管线 |
|---|---|---|---|
| `BackgroundPass` | 画背景：纯色/纹理背景或天空盒 | `EveryCamera` | 全部内置管线 |
| `ShadowMapPass` | 阴影深度预渲染，产出各光源的阴影图 | `Once`（全局一次） | BlinnPhong / PBR Deferred / PBR Forward / CelShading |
| `LightPass` | Blinn-Phong 前向主光照：不透明 + Masked，分静态/骨骼/实例三路 | `EveryCamera` | BlinnPhong。材质覆盖着色器时最常用的 pass key（`ShaderName` 即 `"LightPass"`） |
| `TranslucentPass` | 半透明网格渲染（从远到近排序后混合），继承 `LightPass` | `EveryCamera` | BlinnPhong |
| `ParticlePass` | 广告牌粒子渲染；网格模式发射器走 `InstancedMesh` 由主光照 Pass 绘制 | `EveryCamera` | BlinnPhong / PBR Deferred / PBR Forward / CelShading |
| `GammaCorrectionPass` | 线性色转 sRGB 输出（全屏四边形） | `EveryCamera` | BlinnPhong / NoLight / PointCloud / CelShading / PBR（接在 ToneMapping 之后） |
| `FxaaPass` | FXAA 后处理抗锯齿；`Settings.EnableFxaa = false` 时切 `FXAA_DISABLED` 变体直通 | `EveryCamera` | 全部内置管线（输出到 `CameraOutput`） |
| `ToneMappingPass` | ACES 色调映射，吃 `ToneMappingExposure` 与 `BrightnessClamp` | `EveryCamera` | PBR Deferred / PBR Forward |
| `CopyPass` | 全屏纹理拷贝，把某个 RenderTarget 的颜色纹理搬到另一张（如 PBR 延迟里把光照结果叠到背景目标） | `EveryCamera` | PBR Deferred |
| `DebugDrawPass` | 调试线框叠加：包围盒、光源形状、视锥、骨骼，由 `Settings.Debug.*` 控制 | `EveryCamera` | 全部管线（经 `RegisterDebugPass` 自动注册） |
| `NoLightPass` | 无光照，直接输出材质颜色 | `EveryCamera` | NoLightPipeline |
| `PointCloudPass` | 点云实例化渲染（`gl_PointSize` + 逐实例属性着色） | `EveryCamera` | PointCloudPipeline |

### 管线专属 Pass

| Pass | 所在包 / 管线 | 作用 |
|---|---|---|
| `BasePass` | `Aura3D.Pipeline.PBR`（PBR Deferred） | 把材质写进 GBuffer（BaseColor / NormalRoughness / MetallicEmissive 三张 RGBA8 + 深度） |
| `IBLAmbientPass` / `ConstantAmbientPass` | `Aura3D.Pipeline.PBR` | 延迟光照的环境光项：IBL 卷积环境光 / 常量环境光 |
| `DirectionalLightingPass` / `PointLightingPass` / `SpotLightingPass` | `Aura3D.Pipeline.PBR` | 全屏四边形逐类型延迟光照 |
| `TranslucentPass` / `TranslucentIBLAmbientPass` / `TranslucentConstantAmbientPass` | `Aura3D.Pipeline.PBR` | 半透明的前向补光链 |
| `IrradianceMapPass` / `PrefilteredEnvironmentMapPass` | `Aura3D.Pipeline.PBR.Common` | 由 HDR 环境生成辐照度图与预滤波环境图（IBL 卷积，惰性执行） |
| `CelLightPass` / `OutlinePass` / `CelTranslucentPass` | `Aura3D.Pipeline.CelShading` | 卡通主光照 / 描边 / 卡通半透明 |

各 Pass 的注册顺序与数据流图见 [选择与配置管线](./pipelines.md)；继承 `RenderPass` 自己写见 [自定义渲染管线](./custom-pipeline.md)。

## 着色器宏系统

### 注入点与流程

内置着色器的 GLSL 里有一行 `//{{defines}}`——编译变体时，引擎把当前 defines 列表展开成 `#define X` 若干行并**原地替换**这个注释（顶点、片元两边都替换）。没有这一行，宏传进去也不会生效。

| 方法 | 作用 | 是否碰 GPU |
|---|---|---|
| `UseShader(params string[] defines)` | **替换**当前 defines 列表 | 否 |
| `AddDefines(params string[] defines)` | **追加**到当前列表 | 否 |
| `RemoveDefines(params string[] defines)` | 从列表移除 | 否 |
| `UseShader_Internal()` | 按当前列表编译/取缓存变体并 `glUseProgram` | 是 |

`RenderVisibleMeshesInCamera` 等网格渲染方法会在每个 Mesh 前自动调 `UseShader_Internal`；后处理这类不遍历网格的 Pass 必须在 `RenderQuad()` 前**手动**调用。缓存 key 是 defines 按顺序用 `;` 拼接——顺序不同就是不同变体，建议一次 `UseShader` 声明全部宏。`UseShader`/`AddDefines` 必须发生在 `UseShader_Internal` 之前，否则绑定的是上一帧变体（首帧画空）。

### 常用宏名

以下均为引擎源码中实际使用的宏（大小写照抄）：

| 宏 | 谁注入 | 作用 |
|---|---|---|
| `SKINNED_MESH` | LightPass / TranslucentPass / ShadowMapPass / NoLightPass / Cel / PBR 各 Pass | 蒙皮分支：从 `BoneMatrices` UBO（`MAX_BONES` 256）按骨骼权重变形 |
| `INSTANCED_MESH` | 所有支持实例化的 Pass | `modelMatrix` / `normalMatrix` 从 uniform 改为逐实例顶点属性（location 8–11 / 12–15） |
| `BLENDMODE_MASKED` | LightPass / ShadowMapPass / NoLightPass / Cel / PBR `BasePass` | Alpha 裁剪：`alpha ≤ alphaCutoff` 则 `discard` |
| `BLENDMODE_TRANSLUCENT` | TranslucentPass / NoLightPass / PointCloudPass / Cel / PBR | 半透明分支 |
| `ENABLE_CSM` | BlinnPhong `LightPass`（设了主方向光时）、PBR `DirectionalLightingPass` | 主方向光按级联采样 CSM 阴影图 |
| `ENABLE_SHADOWS` | PBR Deferred 光照 Pass | 该灯型启用阴影采样 |
| `ENABLE_DIR_LIGHT` / `ENABLE_POINT_LIGHT` / `ENABLE_SPOT_LIGHT` | PBR Deferred / PBR Forward 的光照与半透明 Pass | 全屏光照只算这一类灯 |
| `ENBALE_DEFERRED_SHADING` | PBR Deferred（`IBLAmbientPass`、`ConstantAmbientPass`、各光照 Pass） | 延迟光照读 GBuffer 的变体。**注意这是源码拼写，少了一个字母（应为 ENABLE 却写作 ENBALE）**，照抄勿改 |
| `IS_FIRST_LIGHT` | PBR `TranslucentConstantAmbientPass` | 半透明多灯叠加序中的首项处理 |
| `FACE_RENDER` | CelShading `CelLightPass` | 面部专用卡通光照变体 |
| `SKYBOX` / `ORTHOGRAPHIC` / `BACKGROUND_TEXTURE` | `BackgroundPass` | 天空盒 / 天空盒+正交相机 / 纹理背景三种背景变体 |
| `PARTICLE_OPAQUE` / `PARTICLE_MASKED` / `PARTICLE_TRANSLUCENT` | `ParticlePass` | 按发射器 BlendMode 选粒子变体 |
| `PARTICLE_TEXTURE` / `PARTICLE_FLIPBOOK` | `ParticlePass` | 采样粒子贴图 / 再按帧网格切 Flipbook |
| `FXAA_DISABLED` | `FxaaPass` | 关闭抗锯齿时直通拷贝 |

另外，内置着色器头部有编译期常量 `MAX_BONES`、`MAX_DIRECTIONAL_LIGHTS` / `MAX_POINT_LIGHTS` / `MAX_SPOT_LIGHTS`（默认 4）；调整 `PipelineSettings` 的三个光源上限时，`LightPass` 会对源码做字符串替换来改写这几个 `#define`（这就是上限必须在管线创建前设置的原因）。

## 顶点属性与实例属性 location 约定

**一条规则：GLSL 里 `layout(location = N)` 的 N 就是 `BuildInVertexAttribute` 枚举的数值**，引擎按枚举值原样绑定 VAO，没有中间重映射。

| 枚举成员 | location | 说明 |
|---|---|---|
| `Position` | 0 | 顶点位置（vec3） |
| `TexCoord_0` | 1 | 主 UV |
| `Color_0` | 2 | 顶点色（vec4） |
| `Normal` | 3 | 法线 |
| `Tangent` | 4 | 切线 |
| `Bitangent` | 5 | 副切线 |
| `Joints_0` | 6 | 骨骼索引 |
| `Weights_0` | 7 | 骨骼权重 |
| `InstancedTransformColumn0..3` | 8–11 | `INSTANCED_MESH` 下的逐实例 `modelMatrix`（mat4 占 4 个 location） |
| `InstancedNormalTransformColumn0..3` | 12–15 | `INSTANCED_MESH` 下的逐实例 `normalMatrix` |
| `TexCoord_1` | 16 | 自由槽位（常用逐实例自定义属性，如 vec4 实例色） |
| `TexCoord_2` | 17 | 自由槽位 |
| `TexCoord_3` | 18 | 自由槽位 |
| `Joints_1` | 19 | 保留给扩展蒙皮数据 |
| `Weights_1` | 20 | 保留给扩展蒙皮数据 |

> [!WARNING]
> 自定义逐实例属性时，着色器声明的 location 必须与所用枚举的**数值**一致（例如 `SetInstanceAttribute<Vector4>(BuildInVertexAttribute.TexCoord_1, 4, …)` 对应 `layout(location = 16) in vec4 instanceColor;`），别凭印象猜数字。

约定要点：

- location ≤ 7 的内置属性默认启用；不需要的用 `geometry.SetAttributeEnabled(BuildInVertexAttribute.TexCoord_1, false)` 关闭省带宽。实例侧用 `instancedMesh.SetAttributeEnabled("InstanceNormalTransform", false)` 释放 12–15。
- `INSTANCED_MESH` 变体里 `modelMatrix` 是逐实例顶点属性（8–11），不再是非 instanced 的 uniform——两种声明用 `#ifdef INSTANCED_MESH` 分支二选一，内置着色器都这么写。
- 覆盖了顶点着色器就没人替你乘实例矩阵：`#ifdef INSTANCED_MESH` 分支里自己按 location 8 声明并乘进去（参考 Gallery 的 `Kit/Shaders.cs`）。

## 引擎固定提供的 uniform

| uniform 名 | 提供者 | 备注 |
|---|---|---|
| `modelMatrix` / `viewMatrix` / `projectionMatrix` | 渲染方法自动绑定 | 实例变体下 `modelMatrix` 换成顶点属性 |
| `cameraPosition` | 相机 | 视差/环境采样用 |
| `alphaCutoff` | 材质 `AlphaCutoff` | `BLENDMODE_MASKED` 分支使用 |
| `BaseColorTexture` / `NormalTexture` 等 | 材质通道 | 通道名 `BaseColor` → sampler `BaseColorTexture`（PBR 通道同理按名约定） |
| 任意自定义名 | `material.SetParameterValue(name, value)` | 与 GLSL 里同名 uniform 按名绑定 |

## 着色器方言（ShaderDialect）

一句话：**所有内置与自定义着色器按 GLSL ES 3.0（`#version 300 es`）编写**；运行时按上下文自动选方言——ES 上下文原样编译，桌面 GL 上下文把 `#version 300 es` 替换为 `#version 410 core` 并移除全部 `precision` 声明（因此桌面需要 GL 4.1 及以上；macOS 只提供桌面 GL，走此分支）。

内置着色器源码在 [src/Aura3D.Core/Assets/Shaders/](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Assets/Shaders/)，经 `ShaderResource` 暴露为 `MeshVert`/`MeshFrag`（base.vert/base.frag）、`BackgroundVert/Frag`、`ShadowMapVert/Frag`、`NoLightVert/Frag`、`DebugVert/Frag`；FXAA、Gamma、ToneMapping、Copy、Particle、PointCloud 的 GLSL 内嵌在各自 Pass 类中。

## 相关页面

- [自定义材质与着色器](./custom-material.md) — 按 pass key 覆盖材质级着色器
- [自定义渲染管线](./custom-pipeline.md) — 继承 `RenderPass`、宏与 `UseShader_Internal` 细节
- [平台与渲染后端](./platform-render-backends.md) — GLES 3.0 子集与 WebGL2 静默丢弃规则
- [常见坑与排障](./troubleshooting.md) — 按症状索引的坑清单
