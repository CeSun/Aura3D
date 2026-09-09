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

## 示例项目

克隆仓库后运行 `example/Example.Desktop` 查看所有功能的交互式演示：

```shell
git clone https://github.com/CeSun/Aura3d.git
cd Aura3d
dotnet restore
dotnet run --project example/Example.Desktop
```

操作方式：WASD 移动、鼠标右键旋转视角、滚轮缩放、中键平移。左侧菜单可切换不同功能演示页面。
