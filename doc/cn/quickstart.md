---
section: start
order: 1
---

# 快速开始

把 `Aura3DView` 放进你的 Avalonia 工程：一个盒子、一盏光、鼠标环绕，跑起来看到画面。平台与浏览器配置不在此页，见 [平台与渲染后端](./platform-render-backends.md)。

## 安装

```shell
dotnet add package Aura3D.Avalonia
```

`Aura3D.Avalonia` 会自动带上 `Aura3D.Core` 和默认的 BlinnPhong 前向管线——盒子和光照开箱即用，这一步不用装别的。

## 放下控件

在窗口的 XAML 里加命名空间并声明控件，把 GL 初始化完成后的回调挂到 `SceneInitialized`：

```xaml
<Window
    xmlns="https://github.com/avaloniaui"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:a="https://github.com/CeSun/Aura3D"
    x:Class="MyApp.MainWindow">
    <a:Aura3DView
        x:Name="view"
        SceneInitialized="OnSceneInitialized"/>
</Window>
```

`SceneInitialized` 在 OpenGL 初始化完成、`Scene` 就绪后触发——这是建场景的地方，在这里之前碰 GPU 资源都太早。

## 建场景：一个盒子 + 一盏光

代码里在初始化回调中建盒子、加一盏方向光，并启用鼠标环绕：

```csharp
using System.Numerics;
using Aura3D.Avalonia;
using Aura3D.Core.Geometries;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Avalonia.Controls;

public partial class MainWindow : Window
{
    private CameraController? _controller;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnSceneInitialized(object sender, InitializedRoutedEventArgs args)
    {
        var view = (Aura3DView)sender;

        // 一个盒子，放在默认相机前方
        var box = new Mesh { Geometry = new BoxGeometry(), Material = new Material() };
        box.Material.BaseColor = Texture.CreateFromColor(System.Drawing.Color.White);
        box.Position = view.MainCamera.Forward * 3;
        view.AddNode(box);

        // 一盏方向光 —— 没有它画面会全黑（见下方说明）
        var light = new DirectionalLight();
        light.LightColor = System.Drawing.Color.White;
        light.RotationDegrees = new Vector3(-30, -20, 0);   // 用旋转定光照方向
        view.AddNode(light);

        // 鼠标环绕：右键拖拽旋转、滚轮缩放、WASD 移动
        _controller = new CameraController(view);
    }
}
```

> [!IMPORTANT]
> **没有光就什么都看不见。**默认的 BlinnPhong 前向管线是靠光照模型给面着色：场景里一盏灯都没有时，所有面都算成黑色，盒子明明在画面里却看不到。这是新手最常踩的第一个坑——先加一盏 `DirectionalLight` 再排查别的。

`Material` 的 `BaseColor` 是扩展属性，只能在赋值时用（先 `new Material()` 再 `material.BaseColor = ...`），不能写进对象初始化器。注意 `LightColor` 和 `Texture.CreateFromColor(...)` 收的是 `System.Drawing.Color`（不是 Avalonia 的 `Color`），所以上面写成 `System.Drawing.Color.White`。

## 跑起来

按 F5（或 `dotnet run`）。窗口出现后应能看到一个灰色盒子，按住鼠标右键拖拽环绕、滚轮推拉、WASD 平移。

`CameraController` 的常用参数（都在初始化时设）：

| 属性 | 默认 | 作用 |
|---|---|---|
| `MoveSpeed` | `10f` | WASD 移动速度 |
| `MouseSensitivity` | `20f` | 右键旋转灵敏度 |
| `ZoomSpeed` | `5f` | 滚轮缩放速度 |
| `EnableLook` / `EnableZoom` / `EnablePan` / `EnableMovement` | `true` | 分别开关对应操作 |
| `LookButton` / `PanButton` | `Right` / `Middle` | 旋转、平移用哪个鼠标键，可换 |

> `CameraController` 实现了 `IDisposable`，不再用时调 `Dispose()`。

## 平台配置

macOS 桌面工程要把宿主渲染器钉在 OpenGL（视口不出图时先查这里），iOS/Android/Browser 各有后端约束，`net10.0-browser` 的 Release 发布要同时加三项裁剪配置——这些都不影响上面这条最短路径，细节集中在 [平台与渲染后端](./platform-render-backends.md)。

## 画面全黑 / 空白？

十有八九是没加灯，或灯被 `Enable` 关掉、相机没对准物体。逐项排查见 [常见坑与排障](./troubleshooting.md)。

## 下一步

- [第一个完整应用](./first-app.md) — 加载模型、打光、对准相机、逐帧动画、点击拾取，串成一个能跑的小程序
- [场景图与节点](./scene-and-nodes.md) · [光照与阴影](./lighting.md) · [相机与视角控制](./camera.md)
