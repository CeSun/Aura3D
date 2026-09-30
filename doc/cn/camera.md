---
section: basics
order: 3
---

# 相机与视角控制

没有相机，场景里什么都没有。这一篇讲默认相机、透视/正交、对准目标、`CameraController` 交互，以及多相机与渲染到纹理。

## 最短可跑：摆好相机看到场景

每个场景自带一台默认相机 `Scene.MainCamera`（控件上的 `view.MainCamera` 就是它），不需要手动创建：

```csharp
private void OnSceneInitialized(object sender, InitializedRoutedEventArgs args)
{
    var view = (Aura3DView)sender;

    var camera = view.MainCamera;
    camera.Position = new Vector3(0, 5, 10);
    camera.LookAt(new Vector3(0, 0, 0));   // 看向世界原点
}
```

要鼠标键盘交互，再加一个控制器（见下文 CameraController 一节）。

## 投影方式与参数

`ProjectionType` 在透视与正交之间切换，两种投影各有一套参数：

```csharp
var camera = view.MainCamera;

// 透视投影（默认）
camera.ProjectionType = ProjectionType.Perspective;
camera.FieldOfView = 60f;      // 视场角（度）
camera.NearPlane = 0.1f;       // 近裁剪面
camera.FarPlane = 1000f;       // 远裁剪面

// 正交投影（无近大远小，适合 2D 视角、俯视编辑器）
camera.ProjectionType = ProjectionType.Orthographic;
camera.OrthographicSize = 10f; // 正交视图大小
```

| 参数 | 类型 | 默认值 | 生效投影 | 说明 |
|---|---|---|---|---|
| `ProjectionType` | `Perspective` / `Orthographic` | `Perspective` | — | 投影方式 |
| `FieldOfView` | `float` | `75` | 透视 | 垂直视场角（度） |
| `NearPlane` | `float` | `1` | 通用 | 近裁剪面 |
| `FarPlane` | `float` | `100` | 通用 | 远裁剪面，比这更远的东西不渲染 |
| `OrthographicSize` | `float` | `5` | 正交 | 正交视图大小 |
| `IsRenderBackground` | `bool` | `true` | — | 这台相机是否渲染背景/天空盒 |

> [!IMPORTANT]
> 投影参数在**赋值那一刻**校验：`NearPlane > 0`、`FarPlane > NearPlane`、`FieldOfView` 必须在 `(0, 180)` 度之间、`OrthographicSize > 0`，且所有数值必须是有限值——违反任何一条就直接报错。因此当远近裁剪面需要一起大幅改变时（例如从 `Near=1, Far=100` 改成 `Near=200, Far=400`），逐个赋值会经过 `Far < Near` 的非法中间状态，请改用原子更新：
>
> ```csharp
> camera.SetClippingPlanes(200f, 400f);
> ```

## 对准目标与自动取景

不想手算旋转角度时，用这两个方法代替设置 `RotationDegrees`：

```csharp
// 看向一个世界坐标点
camera.LookAt(new Vector3(0, 1f, 0));

// 自动调整位置和朝向，把包围盒整个框进画面（padding 是四周留白比例）
camera.FitToBoundingBox(model.BoundingBox, padding: 0.5f);
```

`FitToBoundingBox` 配合 [模型的 BoundingBox](./models.md#包围盒) 使用，是「加载模型后第一时间看到它」的标准写法。

相机自身的位姿仍然走通用节点属性（`Position`、`RotationDegrees`），见 [场景图与节点](./scene-and-nodes.md)。写自定义着色器时需要相机的矩阵，用只读属性：

```csharp
Matrix4x4 viewMatrix = camera.View;
Matrix4x4 projMatrix = camera.Projection;
Matrix4x4 vpMatrix = camera.ViewProjection;
```

## 让鼠标键盘接管相机：CameraController

`CameraController` 订阅控件输入并驱动 `MainCamera`，默认键位：

- **WASD / QE**：前后左右上下移动
- **右键拖拽**：旋转视角
- **滚轮**：缩放
- **中键拖拽**：平移

```csharp
private CameraController _cameraController;

public void OnSceneInitialized(object sender, InitializedRoutedEventArgs args)
{
    var view = (Aura3DView)sender;

    _cameraController = new CameraController(view)
    {
        MoveSpeed = 30f,          // 移动速度
        MouseSensitivity = 20f,   // 鼠标灵敏度
        ZoomSpeed = 5f,           // 缩放速度
    };
}
```

可配置属性：

| 属性 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `MoveSpeed` | `float` | `10f` | 键盘移动速度 |
| `MouseSensitivity` | `float` | `20f` | 鼠标旋转灵敏度 |
| `PanSpeed` | `float` | `10f` | 平移速度 |
| `ZoomSpeed` | `float` | `5f` | 缩放速度 |
| `Enabled` | `bool` | `true` | 总开关 |
| `EnableLook` | `bool` | `true` | 旋转（右键拖拽） |
| `EnableMovement` | `bool` | `true` | WASD/QE 移动 |
| `EnableZoom` | `bool` | `true` | 滚轮缩放 |
| `EnablePan` | `bool` | `true` | 中键平移 |

> [!NOTE]
> `CameraController` 实现了 `IDisposable`。它挂在控件的输入事件上，不再使用时（页面卸载、切换模式）请调用 `Dispose()`。

## 多相机与分屏

场景中**所有** `Camera` 节点都会被渲染管线自动发现并逐一渲染：注册为 `RenderPassGroup.EveryCamera` 的每个 Pass 会对每台相机执行一次。加第二台相机只需要正常建节点入场景：

```csharp
var secondCamera = new Camera
{
    Position = new Vector3(10, 5, 0),
    IsRenderBackground = false,  // 关键：避免重复渲染天空盒
};
secondCamera.LookAt(Vector3.Zero);

scene.AddNode(secondCamera);
```

> [!TIP]
> 只有一台相机需要负责背景（默认 `IsRenderBackground = true`）。多相机时把其余相机的该属性关掉，否则天空盒会被画多遍，既浪费又可能互相覆盖。

## 渲染到纹理：小地图与监控画面

给相机指定 `RenderTarget` 后，它的画面不进主屏幕而是画进一张纹理，典型用途是小地图、后视镜、监控画面：

```csharp
// 创建离屏渲染目标
var renderTarget = new ControlRenderTarget(width, height);
secondCamera.RenderTarget = renderTarget;

// 渲染完成后，在 SceneUpdated 中读取该纹理作为材质输入，
// 比如贴到一块 PlaneGeometry 上就是画面里的一块小地图
```

管线的 `CameraOutput`、RenderTarget 注册与多 Pass 编排等细节见 [选择与配置管线](./pipelines.md)。

## 常见坑

- **场景空白什么都不出**：先确认相机存在、朝向对着目标——模型不在视线里就用 `LookAt` 或 `FitToBoundingBox` 对准它，再检查光源（见 [光照与阴影](./lighting.md)）。
- **远处的地面/模型被裁掉了**：`FarPlane` 默认只有 100，大场景要抬远（例如 `camera.FarPlane = 250f`）；反过来裁剪面拉太开会损失深度精度、加重 z-fighting。
- **改裁剪面直接抛错**：单独赋值经过非法中间状态（`Far <= Near`）。同时改两端用 `SetClippingPlanes(near, far)`，详见上面校验说明。
- **第二台相机把天空盒又画了一遍**：给它设 `IsRenderBackground = false`。
- **切换页面后键鼠还能操控旧场景**：`CameraController` 没有 `Dispose`，事件还挂在控件上。

## 可运行示例

- [Camera 示例](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/Camera/CameraDemo.axaml.cs) — 透视/正交切换、FOV 与裁剪面滑杆、`LookAt`、`FitToBoundingBox`、世界坐标投影到屏幕的读数验证。

## 下一步

- [选择与配置管线](./pipelines.md) — 多相机渲染在管线层面的工作机制与 `CameraOutput`。
- [环境与背景](./environment.md) — 给 `IsRenderBackground = true` 的相机配一张天空盒。
