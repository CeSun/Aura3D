# Aura3D 文档

欢迎使用 Aura3D——一个基于 Avalonia 的跨平台 3D 渲染控件。文档按「你要做成的那件事」组织：先跑起来，再逐个学会每样能力，出问题查注意事项与速查表。

## 上手

| 文档 | 内容 |
|---|---|
| **[快速开始](./quickstart.md)** | 装包 → 放控件 → 一个盒子 + 一盏光 → 跑起来看到画面 |
| **[第一个完整应用](./first-app.md)** | 加载模型 → 对准相机 → 逐帧动画 → 点击拾取，串成一个能跑的小程序 |

## 场景基础

| 文档 | 内容 |
|---|---|
| **[场景图与节点](./scene-and-nodes.md)** | 节点树与变换 → 该用哪种节点 → 批量修改与查找 |
| **[加载与放置模型](./models.md)** | glTF 加载 → 缩放 / 定位 / 朝向 → 对准相机与包围盒 |
| **[相机与视角控制](./camera.md)** | 投影与 LookAt → CameraController 鼠标环绕 → 多相机与渲染目标 |
| **[光照与阴影](./lighting.md)** | 四类光源 → 光源上限 → 阴影配置与 CSM |
| **[材质与贴图](./material.md)** | 材质通道与贴图 → 加载纹理 → 采样参数 → 材质自定义参数 |
| **[环境与背景](./environment.md)** | 场景背景（纯色 / 贴图 / 立方体图）→ HDR 环境贴图与 IBL |
| **[动画系统](./animation.md)** | 骨骼动画播放与控制 → 混合空间与状态机 → 骨骼手动操作 |
| **[粒子系统](./particle-system.md)** | 发射器配置 → 发射形状 → 网格粒子与 Flipbook → 调试与性能 |

## 进阶

| 文档 | 内容 |
|---|---|
| **[实例化渲染](./instanced-rendering.md)** | InstancedMesh → 逐实例属性 → HISM 层次实例化 → 增量更新 |
| **[自定义材质与着色器](./custom-material.md)** | 材质级着色器替换 → 着色器宏机制 → 逐实例自定义属性 |
| **[选择与配置管线](./pipelines.md)** | 内置管线一览与选择 → PipelineSettings → 光照 / 阴影 / 色调映射参数 |
| **[自定义渲染管线](./custom-pipeline.md)** | 自写 RenderPipeline / RenderPass → 着色器变体与编译 → GLES 3.0 子集约束 |
| **[GPU 资源生命周期](./gpu-resource-lifecycle.md)** | IGpuState 契约 → 按需上传与回收 → 上下文丢失与恢复 |

## 注意事项与速查

| 文档 | 内容 |
|---|---|
| **[平台与渲染后端](./platform-render-backends.md)** | 各平台走哪条渲染路径 → macOS / iOS / Android / Browser 配置 → .NET 10 Release 必填三项 |
| **[常见坑与排障](./troubleshooting.md)** | 按症状索引：黑屏、平台静默失败、着色器、配置时机、上下文与生命周期 |
| **[节点与场景速查](./reference-nodes.md)** | 全部节点类的成员与所在命名空间，供查不供读 |
| **[内置 Pass 与着色器宏速查](./reference-shaders.md)** | 内置 Pass 与 passKey → 顶点属性 location 约定 → 宏组合 |

## 从哪读起

- 第一次用：只读[快速开始](./quickstart.md)就够。
- 画面不对劲：先翻[常见坑与排障](./troubleshooting.md)。
- 发布到某个平台：[平台与渲染后端](./platform-render-backends.md)。
