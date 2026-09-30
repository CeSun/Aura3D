---
section: basics
order: 5
---

# 材质与贴图

材质决定网格的外观：颜色、纹理、透明、正反面。通常不用写着色器——填几张贴图、选一种混合模式就够了。

## 最快让它显示出来

一个网格要能被默认的 Blinn-Phong 管线照亮，至少给它一个带 `BaseColor` 的材质，并且场景里得有光源（见[光照与阴影](./lighting.md)）。

```csharp
var mesh = new Mesh();
mesh.Geometry = new BoxGeometry();

var material = new Material();
material.BaseColor = Texture.CreateFromColor(Color.White);
material.BlendMode = BlendMode.Opaque;

mesh.Material = material;
view.AddNode(mesh);
```

> [!WARNING]
> `BaseColor` 是挂在 `Material` 上的**扩展属性**，不能写进对象初始化器：`new Material { BaseColor = ... }` 编译不过。要一步到位就写 `new Material { BlendMode = BlendMode.Opaque }`，然后单独 `material.BaseColor = ...;`。`BlendMode`、`DoubleSided`、`AlphaCutoff` 是真正的属性，可以直接放进初始化器。

## 三个基本开关

| 属性 | 取值 / 默认 | 作用 |
|---|---|---|
| `BlendMode` | `Opaque` / `Masked` / `Translucent`，默认 `Opaque` | 不透明、镂空裁剪、半透明混合三条渲染路径 |
| `DoubleSided` | `bool`，默认 `false` | 关掉背面剔除，正反两面都画（薄片、布帘、单面面片常用） |
| `AlphaCutoff` | `float`，默认 `0.5` | 仅 `Masked` 用到：贴图 alpha 低于此值的像素被丢弃 |

三种混合模式：

```csharp
material.BlendMode = BlendMode.Opaque;        // 不透明，最省，默认
material.BlendMode = BlendMode.Masked;        // 镂空：按 AlphaCutoff 二选一，留下或丢弃
material.BlendMode = BlendMode.Translucent;   // 半透明：按 alpha 与背景混合
```

`Masked` 适合「要么完全透明、要么完全不透明」的东西（带孔的牌子、铁丝网、树叶）：它靠 `discard` 丢像素、不需要排序，比 `Translucent` 便宜；`Translucent` 才是真正带 alpha 渐变的玻璃、水面。

> [!NOTE]
> `discard`（镂空裁剪）只在 `Masked` 路径下起作用。给一个 `Translucent` 材质去调 `AlphaCutoff` 不会让它变镂空，半透明的柔和边缘也不能指望 `AlphaCutoff` 来裁——两者是不同分支。碰上「透明贴图不透明 / 半透明排序错乱」这类坑，去[常见坑与排障](./troubleshooting.md)。

## 贴图通道

材质用「通道（Channel）」来组织贴图，每个通道是一个 `{ Name, Texture }`。内置管线的着色器按约定好的通道名去采样对应的贴图：

| 通道名 | 含义 | Blinn-Phong | PBR |
|---|---|---|---|
| `BaseColor` | 基础色 / 反照率 | 采样 | 采样 |
| `Normal` | 切线空间法线贴图 | 采样 | 采样 |
| `MetallicRoughness` | 金属度 / 粗糙度打包图（R=金属度、G=粗糙度，glTF 约定） | 不采样 | 采样 |
| `Occlusion` | 环境光遮蔽 | 不采样 | 会绑定、但着色器不采样 |
| `Emissive` | 自发光 | 不采样 | 仅延迟管线的不透明分支采样 |

设置 / 读取通道有几种写法：

```csharp
// 1) 便捷扩展属性（只有 BaseColor / Normal 提供了扩展属性）
material.BaseColor = Texture.CreateFromColor(Color.White);
material.Normal = normalTexture;

// 2) 按通道名设置 / 读取（所有通道通用）
material.SetTexture("MetallicRoughness", metallicRoughnessTexture);
material.SetTexture("Normal", null);          // 传 null 即移除该通道
var t = material.GetTexture("Normal");         // 没有则返回 null

// 3) 直接操作 Channel 对象
material.SetChannel(new Channel { Name = "Emissive", Texture = emissiveTexture });
material.SetChannels(new[]
{
    new Channel { Name = "BaseColor", Texture = baseColorTexture },
    new Channel { Name = "Normal",    Texture = normalTexture },
});   // SetChannels 会先清空原有通道，再整体替换
```

> [!WARNING]
> `material.Channels` 是**只读**的（`IReadOnlyList<Channel>`），只能读、不能赋值——不要写 `material.Channels = new List<Channel>{...}`。要增删通道请用 `SetChannel` / `SetChannels` / `SetTexture`。

`BaseColor` / `Normal` 这两个扩展属性只是 `SetTexture("BaseColor", …)` / `SetTexture("Normal", …)` 的语法糖，底层就是通道，两种写法可以混用。至于自定义通道会被你的着色器当成 `xxxTexture` uniform 来采样，见[自定义材质与着色器](./custom-material.md)。

## 加载纹理

贴图对象从 `TextureLoader` 或 `Texture` 的静态方法得到：

```csharp
using var stream = File.OpenRead("brick.png");
var albedo = TextureLoader.LoadTexture(stream);          // 普通 2D 图

using var hdr = File.OpenRead("studio.hdr");
var hdri = TextureLoader.LoadHdrTexture(stream);         // HDR 环境图

// 纯色贴图，无需文件，占位 / 单色材质最方便
var flat = Texture.CreateFromColor(Color.FromArgb(255, 200, 50, 50));
```

加载完直接把返回值填进通道即可：`material.SetTexture("BaseColor", albedo);`。立方体贴图（`LoadCubeTexture`）与 HDR 转立方体主要用于环境与背景，见[环境与背景](./environment.md)。

## 采样配置

一张 `Texture` 上还能配包裹、过滤、色彩空间（`SetWrapS` / `SetWrapT` / `SetMinFilter` / `SetMagFilter` / `SetColorFormat` / `SetIsGammaSpace`），它们直接决定贴图接缝会不会发黑、缩小时有没有摩尔纹、颜色对不对。这套参数与场景背景、环境贴图共用，集中讲在[环境与背景](./environment.md)，这里不重复——你只要记住：填进通道前，可以先对 `Texture` 链式调用这些方法再赋给材质。

## 材质参数（喂给自定义着色器）

`SetParameterValue` 往材质上挂一个「按名字绑定」的值。内置管线一般用不到它，但当你给材质写了自定义着色器时，同名 uniform 会自动取到这些值：

```csharp
material.SetParameterValue("uColor", new Vector4(1f, 0.3f, 0.2f, 1f));  // vec4
material.SetParameterValue("uTime", 0f);                                // float

if (material.TryGetParameterValue("uTime", out float time))
{
    material.SetParameterValue("uTime", time + 0.016f);                 // 每帧推进
}

material.RemoveParameterValue("uColor");
```

值的类型决定它绑到哪种 uniform：`int` / `float` / `Vector2` / `Vector3` / `Vector4` / `Matrix4x4`。完整用法（连同怎么覆盖某个 Pass 的 GLSL）在[自定义材质与着色器](./custom-material.md)。

## 复制一个材质

想改一份已有材质、又保留原件时，用克隆：

```csharp
var copy = material.Clone();                        // 通道里的 Texture 与原材质共用同一对象
var variant = material.DeepClone();                 // 每个通道各自克隆一份 Texture（采样配置独立、像素仍共享）
var independent = material.DeepClone(deepCopyTextures: true); // 连贴图像素数据也各自一份
```

| 方法 | 开关 / 通道 / 参数 | 通道里的 Texture |
|---|---|---|
| `Clone()` | 全部复制 | 共用同一对象——改一个会牵动另一个 |
| `DeepClone()`（默认） | 全部复制 | 各自浅克隆：采样配置独立，像素数据仍共享 |
| `DeepClone(deepCopyTextures: true)` | 全部复制 | 各自深克隆：像素数据也各存一份 |

想省事、又不在乎两份材质共享贴图，就选 `Clone()`；要彻底独立（例如会各自改写像素）才用 `DeepClone(deepCopyTextures: true)`。

## 常见坑

- `new Material { BaseColor = ... }` 编译不过：`BaseColor` 是扩展属性，只能赋值、不能进初始化器。
- `material.Channels = ...` 无效：`Channels` 只读，请用 `SetChannel` / `SetChannels` / `SetTexture`。
- 模型一片漆黑或不显示：多半是场景里没光源或没相机，不一定是材质的锅，回看[光照与阴影](./lighting.md)。
- 给 `Translucent` 调 `AlphaCutoff` 想裁边、或 `Masked` 想要柔和边缘：模式选错了，二者是两条路径，详见[常见坑与排障](./troubleshooting.md)。
- 贴图接缝处发黑、缩小满屏噪点：是采样配置（Wrap / Filter）问题，去[环境与背景](./environment.md)对照。

## 看看实际效果

Gallery 里这两个示例直接照抄最快：
- **PbrMaterials** — 三档典型实拍 PBR 材质（锈蚀金属/砖墙/镀锌钢板）按 BaseColor / Normal / ARM 打包的 MetallicRoughness 接线，在真实 IBL 下渲染。
- **MaterialShaders** — `SetTexture`、材质参数，以及只覆盖片元着色器的自定义材质。

源码：[Material.cs](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Resources/Material.cs)、[MaterialExtensions.cs](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Resources/MaterialExtensions.cs)。
