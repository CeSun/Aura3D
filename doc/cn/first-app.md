---
section: start
order: 2
---

# 第一个完整应用

[快速开始](./quickstart.md) 让你看到了第一个画面。这一页把零散积木串成一个能跑的小程序：加载一个 glTF 模型，摆好位置，打光，把相机对准它，在 `SceneUpdated` 里逐帧自转，再用鼠标点击拾取某个部件并高亮它。照着敲一遍，你就掌握了 Aura3D 应用的主干。

做完你会得到：一个自动对准、缓慢自转、可以用鼠标环绕、点哪个部件哪个部件变红的模型查看器雏形。

## 装包

模型加载不在基础包里，需要额外装 glTF 加载器：

```shell
dotnet add package Aura3D.Avalonia
dotnet add package Aura3D.Model.GltfLoader
```

准备一个 `.glb` 模型文件（叫它 `model.glb`），放到程序运行目录，或把下面代码里的路径换成你的绝对路径。

## XAML：一个视图，三个回调

```xaml
<Window
    xmlns="https://github.com/avaloniaui"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:a="https://github.com/CeSun/Aura3D"
    x:Class="MyApp.MainWindow">
    <a:Aura3DView
        x:Name="view"
        SceneInitialized="OnSceneInitialized"
        SceneUpdated="OnSceneUpdated"
        PointerPressed="OnPointerPressed"/>
</Window>
```

- `SceneInitialized` — GL 就绪、`Scene` 可用，在这里建场景、加载模型、对准相机。
- `SceneUpdated` — 每帧渲染前触发，参数带 `DeltaTime`（秒），逐帧动画放这里。
- `PointerPressed` — Avalonia 控件的标准指针事件，`Aura3DView` 就是个控件，直接用它拿到点击位置做拾取。

## 完整代码

`MainWindow.axaml.cs`：

```csharp
using System;
using System.Numerics;
using Aura3D.Avalonia;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Aura3D.Model;
using Avalonia.Controls;
using Avalonia.Input;

public partial class MainWindow : Window
{
    private const string ModelPath = "model.glb";   // 换成你自己的模型

    private CameraController? _controller;
    private Model? _model;

    // 记录上一次被高亮的部件，点击新部件时先还原它
    private Mesh? _highlighted;
    private Texture? _savedBaseColor;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnSceneInitialized(object sender, InitializedRoutedEventArgs args)
    {
        var view = (Aura3DView)sender;
        var scene = args.Scene;

        scene.Background = Texture.CreateFromColor(System.Drawing.Color.Gray);

        // 1) 加载模型
        var model = ModelLoader.LoadGlbModel(ModelPath);
        model.Name = "Hero";

        // 2) 摆位置：放在世界原点，正立朝向相机
        model.Position = new Vector3(0, 0, 0);

        // 3) 打一盏方向光 —— 默认 BlinnPhong 没光就全黑
        var light = new DirectionalLight();
        light.LightColor = System.Drawing.Color.White;
        light.RotationDegrees = new Vector3(-30, -20, 0);
        view.AddNode(light);

        // 4) 相机对准：按模型包围盒自动取景，padding 留一点呼吸空间
        view.MainCamera.FitToBoundingBox(model.BoundingBox, padding: 0.5f);

        view.AddNode(model);
        _model = model;

        // 5) 鼠标环绕
        _controller = new CameraController(view) { MoveSpeed = 20f };
    }

    private void OnSceneUpdated(object sender, UpdateRoutedEventArgs e)
    {
        if (_model == null)
            return;

        // 每帧绕 Y 轴转一点：30 度/秒 × 本帧时长
        _model.RotationDegrees += new Vector3(0, 30, 0) * (float)e.DeltaTime;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var view = (Aura3DView)sender!;
        var scene = view.Scene;
        if (scene == null)
            return;

        // 只在左键点击时拾取，避免和右键环绕冲突
        if (!e.GetCurrentPoint(view).Properties.IsLeftButtonPressed)
            return;

        var position = e.GetPosition(view);
        var hit = scene.PickClosest((float)position.X, (float)position.Y, view.MainCamera);
        if (hit == null)
            return;

        if (hit.Value.Node is not Mesh mesh || mesh.Material == null)
            return;

        // 先还原上一次的高亮
        if (_highlighted?.Material != null)
            _highlighted.Material.SetTexture("BaseColor", _savedBaseColor);

        // 记下原始颜色、把命中的部件涂红
        _highlighted = mesh;
        _savedBaseColor = mesh.Material.GetTexture("BaseColor");
        mesh.Material.SetTexture("BaseColor", Texture.CreateFromColor(System.Drawing.Color.Red));

        view.RequestNextFrameRendering();
    }
}
```

> `ModelLoader` 由 `Aura3D.Model.GltfLoader` 包提供，命名空间 `Aura3D.Model`——若编译器找不到它，说明还没装那个包。
>
> `LightColor` 和 `Texture.CreateFromColor(...)` 用的是 `System.Drawing.Color`，不是 Avalonia 的 `Avalonia.Media.Color`。上面写成 `System.Drawing.Color.White` 是为了避免和 Avalonia 的同名 `Color` 混淆；如果你的文件里已经 `using` 了 `Avalonia.Media`，就照这样全限定写。

## 逐段在做什么

1. **加载** — `ModelLoader.LoadGlbModel(path)` 返回一个 `Model` 节点树，里面是若干 `Mesh` 部件。要连动画一起取用 `LoadGlbModelAndAnimations`。
2. **摆位置** — `Model` 继承自 `Node`，和盒子里用的一样有 `Position`/`RotationDegrees`/`Scale`。
3. **打光** — 一盏 `DirectionalLight` 就够看到模型；`RotationDegrees` 决定光从哪来。
4. **对准** — `FitToBoundingBox(model.BoundingBox, padding)` 让相机退到刚好框住整个模型，省去手调距离。模型尺度未知时这一步尤其重要。
5. **逐帧自转** — `SceneUpdated` 每帧触发，用 `e.DeltaTime` 换算角速度，转得与帧率无关。
6. **点击拾取** — `Scene.PickClosest(x, y, camera)` 返回该屏幕点最近的命中 `PickResult?`，`hit.Value.Node` 就是被点到的 `Mesh`；用 `SetTexture("BaseColor", ...)` 换它的颜色做高亮，记住还原上一件。

## 常用选项

- **只想按需渲染**（静止时省电）：`view.AutoRequestNextFrameRendering = false;`，然后在动画或点击后调 `view.RequestNextFrameRendering()` 推一帧。本例用默认的自动逐帧。
- **拾取全部命中**：`scene.Pick(x, y, camera)` 返回按距离排序的 `List<PickResult>`，可穿过半透明挡板拿后面那件；`PickClosest` 只拿最近一件。
- **环绕手感**：`CameraController` 的 `MouseSensitivity`、`ZoomSpeed`、`PanSpeed`，或用 `EnableZoom`/`EnablePan` 关掉对应操作。
- **取景更紧/更松**：调 `FitToBoundingBox` 的 `padding`，或改 `view.MainCamera.FieldOfView`。

> [!WARNING]
> 默认前向管线每种光源最多 4 盏，超出的会被忽略。需要更多灯或物理光照，参考 [选择与配置管线](./pipelines.md)。

## 常见坑

- **画面全黑**：九成是没加灯，或 `RotationDegrees` 把方向光转到背对模型、相机没对准。先确认加了一盏方向光。
- **模型巨大/极小、相机跑很远**：这是正常现象，`FitToBoundingBox` 会按模型实际尺寸把相机拉开；想看细节就滚轮推近。
- **点击拾取有偏移（高 DPI）**：`e.GetPosition(view)` 给的是逻辑像素。若命中点和实际有偏移，参考 [常见坑与排障](./troubleshooting.md) 里的坐标换算。
- **`BaseColor` 不能写进初始化器**：它是扩展属性，`mesh.Material = new Material { BaseColor = ... }` 编译不过；先 new 再赋值，或用 `SetTexture("BaseColor", ...)`。
- **高亮没还原就点下一件**：本例每次点击先把上一件的颜色 `SetTexture` 回去，否则点过的部件会一直红着。

## 想看更多真实写法

- [Picking 示例](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/Picking/PickingDemo.axaml.cs) — `ObjectPicked` 事件、全命中列表、实例拾取带回 `InstanceIndex`
- [ModelViewer 示例](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/ModelViewer/ModelViewerDemo.axaml.cs) — 模型树、包围盒、`Clone(CopyType)` 的共享边界
- [Camera 示例](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/Camera/CameraDemo.axaml.cs) — 投影切换、`LookAt`、`FitToBoundingBox`、`WorldToScreen`

## 下一步

按主题深入：[场景图与节点](./scene-and-nodes.md) · [加载与放置模型](./models.md) · [相机与视角控制](./camera.md) · [光照与阴影](./lighting.md) · [材质与贴图](./material.md)。动画见 [动画系统](./animation.md)。
