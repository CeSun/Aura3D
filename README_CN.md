<div id="header" align="center">
    <img width="200px" src="./logo.svg" ></img> 
    <h4><i>轻量级、可扩展、高性能的 Avalonia 3D 控件库</i></h4>
    <div id="link">
        <a href="./README.md">English</a> | 
        <span>中文</span> |
        <a href="https://cesun.github.io/Aura3D-Docs/">文档</a> |
        <a href="https://cesun.github.io/Aura3D-Gallery/">在线演示</a>
    </div>
</div>
<br/>

![demo](./doc/images/example_debugtest.png)

**在线示例库：** [Aura3D-Gallery](https://cesun.github.io/Aura3D-Gallery/) —— 在浏览器里直接运行引擎各项能力（渲染管线、IBL、动画混合空间、材质系统）。

**Demo 项目：** [TowerDefense3D](https://github.com/CeSun/TowerDefense3D) — 基于 Aura3D 开发的塔防游戏，展示了引擎的实际应用。

> [!IMPORTANT]
> 项目正在积极开发中，欢迎通过 [Issue](https://github.com/CeSun/Aura3d/issues) 提交建议和反馈。

## 简介

Aura3D 是一个轻量级、高性能、可扩展的 Avalonia 3D 控件库。它覆盖了从模型加载、场景管理、光照阴影到自定义渲染管线的完整链路，适合在 .NET 桌面应用中集成 3D 内容。

## 特性

| 分类 | 能力 |
|---|---|
| 场景与模型 | 场景图与层次化节点树；原生加载 glTF/GLB，通过 Assimp 支持 FBX、OBJ、3DS 等 50+ 格式；内置盒子 / 球体 / 圆柱 / 平面 |
| 相机 | 透视与正交投影、对准目标与自动取景、`CameraController` 键鼠交互、多相机分屏、渲染到纹理 |
| 光照与阴影 | 方向光 / 点光 / 聚光灯；主方向光 CSM 级联阴影（级联数与分割方案可配）；默认 Blinn-Phong 前向管线 |
| 环境与 IBL | 纯色 / 贴图 / 立方图背景；HDR 全景天空盒；PBR 管线烘培辐照度图与预滤波环境贴图 |
| 材质与着色器 | 材质通道与贴图及其采样参数；材质级自定义着色器与着色器宏；逐实例自定义属性 |
| 动画 | glTF 蒙皮与 Assimp 外部动画；2D 动画混合空间；基于条件的动画状态图；骨骼挂点 |
| 粒子 | CPU 模拟粒子系统，发射形状、网格粒子与 Flipbook、完整播放生命周期 |
| 渲染管线 | 内置 BlinnPhong / NoLight / PBR 延迟 / PBR 前向 / 卡通（默认管线基于 OpenGL ES 3.0）；自由组合 RenderPass 的自定义管线，无需处理 VAO/VBO |
| 实例化与拾取 | `InstancedMesh` GPU 实例化、`InstancedMeshGroup` 层次化实例化（HISM，增量更新与自动分组）、点云、全部图元拓扑；`Scene.Pick` 三角形级拾取 |
| 性能与调试 | 视锥体剔除、八叉树空间索引、包围盒 / 光源 / 骨骼 / 摄像机视锥体调试绘制 |
| GPU 资源 | 按需上传与回收，上下文丢失自动恢复 |
| 平台 | Avalonia：Windows / Linux / macOS / Android / iOS；.NET 8.0 与 .NET 10.0，含浏览器（WebGL2） |

## 快速开始

### 1. 安装

```shell
dotnet add package Aura3D.Avalonia
dotnet add package Aura3D.Model.GltfLoader
```

> [!IMPORTANT]
> **Browser（`net10.0-browser`）项目：**.NET 10 下 Release/静态发布需要在应用 `.csproj` 中额外
> 加入三项 MSBuild 配置，详见
> [Browser Release/静态发布说明](https://cesun.github.io/Aura3D-Docs/platform-render-backends.html#browser-net10-release-config)。

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

> 更多特性的使用方法请参阅[中文文档](https://cesun.github.io/Aura3D-Docs/)。

## NuGet 包

| 包名 | 说明 |
|---|---|
| [Aura3D.Avalonia](https://www.nuget.org/packages/Aura3D.Avalonia) | Avalonia 3D 渲染控件（依赖 Aura3D.Core） |
| [Aura3D.Avalonia.Browser](https://www.nuget.org/packages/Aura3D.Avalonia.Browser) | 浏览器（WebGL2）渲染路径的构建期接线包 |
| [Aura3D.Core](https://www.nuget.org/packages/Aura3D.Core) | 核心引擎：场景图、节点、资源、默认管线 |
| [Aura3D.Model.GltfLoader](https://www.nuget.org/packages/Aura3D.Model.GltfLoader) | glTF/GLB 模型加载器 |
| [Aura3D.Model.AssimpLoader](https://www.nuget.org/packages/Aura3D.Model.AssimpLoader) | Assimp 模型加载器（支持 50+ 格式） |
| [Aura3D.Pipeline.PBR](https://www.nuget.org/packages/Aura3D.Pipeline.PBR) | PBR 延迟渲染管线 |
| [Aura3D.Pipeline.PBRForward](https://www.nuget.org/packages/Aura3D.Pipeline.PBRForward) | PBR 前向渲染管线（尚未发布 NuGet，暂从源码引用） |
| [Aura3D.Pipeline.CelShading](https://www.nuget.org/packages/Aura3D.Pipeline.CelShading) | 卡通渲染管线 |
| [Aura3D.Angle.iOS](https://www.nuget.org/packages/Aura3D.Angle.iOS) | iOS 渲染路径的 ANGLE (Metal) 原生框架包 |

## 许可证

[MIT](LICENSE)
