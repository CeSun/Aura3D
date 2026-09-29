# Aura3D 文档

欢迎使用 Aura3D。本文档覆盖从安装配置到自定义渲染管线的完整用法。

## 文档

| 文档 | 内容 |
|---|---|
| **[开始上手](./get-started.md)** | 安装 NuGet 包 → XAML 声明控件 → 初始化场景 → 摄像机配置 → 加载模型 → 设置光源 → 场景背景 → 渲染循环控制 |
| **[渲染管线](./pipelines.md)** | 内置 BlinnPhong / NoLight / PBR / CelShading 管线 → 自定义 RenderPipeline + RenderPass → 着色器宏系统 → 生命周期钩子 → 多摄像机 |
| **[动画系统](./animation.md)** | 骨骼动画 / 循环模式 / 外部动画导入 → 2D 动画混合空间 → 动画状态图 → 骨骼手动操作 |
| **[实例化渲染](./instanced-rendering.md)** | GPU 实例化（InstancedMesh）→ 逐实例属性 / 更新变换 → 层次化实例化（HISM）→ 增量更新 |
| **[渲染专题](./rendering.md)** | 阴影 / 点云 / 图元渲染 → 材质高级用法 → 节点操作（包围盒 / 克隆 / 批量变换 / 查找子节点） |
| **[GPU 资源生命周期](./gpu-resource-lifecycle.md)** | GPU 状态所有权 → 重复销毁 → 上下文丢失与恢复 → 自定义状态契约 |
| **[平台与渲染后端](./platform-render-backends.md)** | 各平台走哪条渲染路径 → iOS 的 ANGLE(Metal) 后端与 framework 准备 → GLES 3.0 子集限制 → 帧调度与线程语义 |

## 示例项目

克隆仓库后运行 `example/Example.Desktop` 查看所有功能的交互式演示：

```shell
git clone https://github.com/CeSun/Aura3d.git
cd Aura3d
dotnet restore
dotnet run --project example/Example.Desktop
```

操作方式：WASD 移动、鼠标右键旋转视角、滚轮缩放、中键平移。左侧导航在 21 个功能页之间切换。

示例项目的四条结构约定：

- **页目录是唯一登记表**：`example/Aura3D.Example.Shared/Demos/DemoRegistry.cs` 一项一页，导航、深链、体积预算都从它读，新增一页不必改宿主。
- **资产在程序集之外**：`example/assets`（入库的是压缩产物）按功能页懒加载，绝不走 `<AvaloniaResource>`——那等于把整包字节编进 wasm。原始大文件放在 gitignore 的 `example/assets-src`，`node tools/assets/generate.mjs` 负责重编码，`--check` 只核对预算（浏览器侧合计 ≤ 20 MB、单页进页即取 ≤ 6 MB、清单里没有无人取用的 Key），CI 每次跑这条。
- **深链**：桌面 `--page=<Id>`、浏览器 `?page=<Id>`，Id 见上面那张表。
- **文案一律进 resx，中英两份**：`Localization/Strings.resx`（英文）与 `Strings.zh-Hans.resx`（中文），经 [Irihi.Lingua](https://github.com/irihitech/Irihi.Lingua) 读取——AXAML 里写 `{Translate {x:Static loc:Strings+Keys.X}}`，C# 里写 `Strings.Keys.X.T()` / `.Format(...)`。页头按钮即时切换语言并重建当前页；启动语言由 `--lang=` / `?lang=`（或 `AURA3D_LANG`）指定，缺省跟随系统界面语言。`python3 tools/i18n/check.py` 在 CI 里守住这条：两份键集合一致、属性值与字符串字面量里没有写死中文、`Format` 实参与占位符对得上、下拉选项翻译后仍彼此可辨。
