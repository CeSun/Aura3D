<div id="header" align="center">
    <img width="200px" src="./logo.svg" ></img> 
    <h4><i>轻量级、可扩展、高性能的 3D 渲染控件</i></h4>
    <div id="link">
        <a href="./README.md">English</a> | 
        <span>中文</span> |
        <a href="./doc/cn/home.md">文档</a>
    </div>
</div>
<br/>

![demo](./doc/images/example_debugtest.png)

**Demo 项目：** [TowerDefense3D](https://github.com/CeSun/TowerDefense3D) — 基于 Aura3D 开发的塔防游戏，展示了引擎的实际应用。

> [!IMPORTANT]
> 项目正在积极开发中，欢迎通过 [Issue](https://github.com/CeSun/Aura3d/issues) 提交建议和反馈。

## 简介

Aura3D 是一个基于 Avalonia 的 3D 渲染控件，底层使用 OpenGL ES 3.0。它提供了从模型加载、场景管理、光照阴影到自定义渲染管线的完整能力，适合在 .NET 桌面应用中集成 3D 内容展示。

## 特性

### 场景与模型
- **多格式模型加载** — 原生支持 glTF/GLB，通过 Assimp 扩展支持 FBX、OBJ、3DS 等 50+ 格式
- **内置基础几何体** — 盒子、球体、圆柱体、平面
- **场景图** — 层次化的节点树，支持父子变换继承

### 光照与阴影
- **方向光 / 点光 / 聚光灯** — 三种光源类型，支持颜色、衰减半径、阴影投射
- **CSM 级联阴影** — 主方向光自动使用级联阴影贴图，可配置级联数量和分割方案
- **Blinn-Phong 光照模型** — 默认前向渲染管线
- **HDR 环境贴图** — 天空盒 / 环境光背景

### 动画系统
- **骨骼动画** — 支持 glTF 蒙皮动画和 Assimp 导入的外部动画
- **动画混合空间** — 2D 混合空间，在多个动画间平滑过渡
- **动画状态图** — 基于条件的状态机，支持状态间的混合过渡

### 渲染管线
- **可替换渲染管线** — 内置 BlinnPhong（写实）、NoLight（无光照）
- **PBR 延迟管线** — 基于物理的渲染，支持 Metallic-Roughness 工作流
- **卡通渲染管线** — Cel Shading 风格
- **自定义管线** — 自由组合 RenderPass，无需处理 VAO/VBO

### 高级渲染
- **GPU 实例化** — `InstancedMesh`，万级实例高性能渲染
- **层次化实例化** — `InstancedMeshGroup`（类似 UE 的 HISM），支持增量更新与自动分组
- **视锥体剔除** — 可开关，大幅减少不可见物体的绘制
- **八叉树空间索引** — 自动扩张的八叉树，高效空间查询与剔除
- **点云渲染** — 内置 `PointCloudPipeline`，基于实例化的高性能点云渲染
- **图元渲染** — Triangles、Lines、LineStrip、LineLoop、Points、TriangleStrip、TriangleFan
- **点击拾取** — `Scene.Pick` / `Scene.PickClosest`，支持三角形级别的 Mesh 和 InstancedMesh 拾取
- **调试可视化** — 内置调试绘制，支持包围盒、光源、骨骼、摄像机视锥体等可视化

### 平台
- **Avalonia** — 支持 Windows、Linux、macOS、Android、iOS
- **.NET 8+** — 支持 .NET 8.0 和 .NET 10.0

## 快速开始

### 1. 安装

```shell
dotnet add package Aura3D.Avalonia
dotnet add package Aura3D.Model.GltfLoader
```

> 至少需要安装一个模型加载库。`Aura3D.Model.GltfLoader` 用于加载 glTF/GLB 格式。
> 如需加载 FBX、OBJ、3DS 等 50+ 格式，请额外安装 `Aura3D.Model.AssimpLoader`。

### 2. 在 XAML 中使用

```xaml
<Window
    xmlns:a="https://github.com/CeSun/Aura3D"
    ...>
    <a:Aura3DView x:Name="aura3Dview" SceneInitialized="OnSceneInitialized"/>
</Window>
```

### 3. 加载一个模型

```csharp
public void OnSceneInitialized(object sender, InitializedRoutedEventArgs args)
{
    var view = (Aura3DView)sender;
    var camera = view.MainCamera;

    // 设置背景色
    view.Scene.Background = Texture.CreateFromColor(Color.Gray);

    // 加载 glTF/GLB 模型
    var model = ModelLoader.LoadGlbModel("model.glb");
    model.Position = camera.Forward * 3;
    view.AddNode(model);

    // 添加方向光（默认管线需要光源才能看到模型）
    var dl = new DirectionalLight();
    dl.RotationDegrees = new Vector3(-30, 0, 0);
    dl.LightColor = Color.White;
    view.AddNode(dl);
}
```

> 更多特性的使用方法请参阅 [文档目录](doc/cn/home.md)。

## NuGet 包

| 包名 | 说明 |
|---|---|
| [Aura3D.Avalonia](https://www.nuget.org/packages/Aura3D.Avalonia) | Avalonia 3D 渲染控件（依赖 Aura3D.Core） |
| [Aura3D.Core](https://www.nuget.org/packages/Aura3D.Core) | 核心引擎：场景图、节点、资源、默认管线 |
| [Aura3D.Model.GltfLoader](https://www.nuget.org/packages/Aura3D.Model.GltfLoader) | glTF/GLB 模型加载器 |
| [Aura3D.Model.AssimpLoader](https://www.nuget.org/packages/Aura3D.Model.AssimpLoader) | Assimp 模型加载器（支持 50+ 格式） |
| [Aura3D.Pipeline.PBR](https://www.nuget.org/packages/Aura3D.Pipeline.PBR) | PBR 延迟渲染管线 |
| Aura3D.Pipeline.PBR.Common | PBR 渲染管线共享资源 |
| Aura3D.Pipeline.PBRForward | PBR 前向渲染管线 |
| [Aura3D.Pipeline.CelShading](https://www.nuget.org/packages/Aura3D.Pipeline.CelShading) | 卡通渲染管线 |

## 许可证

[MIT](LICENSE)
