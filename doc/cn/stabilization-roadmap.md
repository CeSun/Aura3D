# Aura3D 稳定化路线图

本文档记录框架从 Alpha 走向可发布版本所需的稳定化工作。完成状态以代码、自动化测试和 CI 结果为准。

## 第一阶段：发布阻断项

- [x] HISM 异步重建增加数据版本校验，只允许最新快照在主线程提交。
- [x] HISM 取消和异常路径统一收尾，并为构建期间的实例变更增加回归测试。
- [x] 修复 glTF 文件重载遗漏 Skeleton 参数的问题。
- [x] 修复 Cel 材质纹理索引的上界判断。
- [x] 材质扩展注册表改为并发安全实现。
- [x] 粒子停止或离开 Scene 时主动释放 GPU Buffer。
- [x] RenderTarget 销毁改为幂等，并补齐 Pipeline 缓存与集合清理。
- [x] 发布流程补齐 PBR.Common 和 PBRForward，同时在 PR、main 推送时执行测试和打包验证。

验收标准：核心测试全部通过；所有可发布库在 net8.0 和 net10.0 下构建成功；PBR 包的同版本依赖都存在于发布产物中。

## 第二阶段：行为与 API 契约

- [x] AnimationSampler 和 AnimationGraph 使用 deltaTime，不再读取系统墙钟。
- [x] 动画过渡按 TRS 分解混合，旋转使用 Quaternion.Slerp。
- [x] Pipeline 的节点和 Pass 集合改为只读公开接口。
- [x] 禁止直接替换 Scene 的 RenderPipeline 和 MeshOctree。
- [x] HISM、动画时间缩放和 BlendSpace IDW 参数增加基础校验。
- [x] InstancedMesh 批量替换实例时同步刷新拾取包围盒。
- [x] Shader 编译、链接失败时释放临时 GL 对象。
- [ ] 为 Camera、PipelineSettings 和 ParticleEmitter 建立一致的参数校验规则。
- [ ] 定义并文档化所有 GPU 资源的所有权、上下文丢失与重复销毁语义。

验收标准：固定 deltaTime 输入产生确定结果；外部调用者无法绕过 Scene 修改 Pipeline 注册集合；资源释放路径可重复调用。

## 第三阶段：性能与跨平台

- [x] 普通 Mesh 拾取使用场景八叉树作为宽阶段。
- [x] 移除 billboard 粒子模拟阶段的重复数据打包。
- [ ] 为 HISM、拾取、透明粒子和多相机渲染建立 BenchmarkDotNet 基线。
- [ ] 启动时探测 GL 版本、扩展、纹理尺寸、附件数量和 Uniform 限制。
- [ ] 为 RGBA32F、ClampToBorder 等非基础 GLES 能力提供降级策略。
- [ ] 增加真实 OpenGL 上下文的离屏渲染测试与截图基准。
- [ ] 建立 Windows、Linux、macOS 平台矩阵，并为 Android、iOS 增加设备或模拟器烟雾测试。

验收标准：支持的平台都有自动化证据；不支持的 GPU 能力会产生明确诊断或安全降级；性能回归可被 CI 发现。

## 持续质量门禁

- 测试和打包必须先于 NuGet 发布成功。
- 新增缺陷修复必须附带可重复的回归测试。
- 逐步清理 nullable、分析器、XML 文档和 NU1507 警告，并在清零后启用 warnings-as-errors。
- 公共 API 的破坏性调整必须记录迁移说明。
