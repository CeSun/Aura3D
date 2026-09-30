# Aura3D.Angle.iOS

[English](./README.md) | 中文

[Aura3D](https://github.com/CeSun/Aura3d) 的 iOS 渲染路径自带 ANGLE（Metal 后端）上下文，
但 ANGLE 的原生库必须由**应用**链接进主可执行文件：`Aura3D.Avalonia` 里的桥接代码用
`DllImport("__Internal")` 解析符号（为了避开 Apple 自带的 OpenGLES 符号混用），
所以库不能只放在类包里。这个包就是替应用做这件事：装上之后 targets 自动注入
`NativeReference`，不需要写任何配置。

## 用法

不需要单独装。`Aura3D.Avalonia` 的 iOS 目标以精确区间依赖本包，包里的 `buildTransitive` targets
会穿透到应用工程生效，所以应用侧一行 ANGLE 配置都不用写：

```shell
dotnet add package Aura3D.Avalonia
```

想显式控制切片版本时再直接引用本包也可以：

```shell
dotnet add package Aura3D.Angle.iOS
```

只作用于 iOS 目标框架（`$(TargetFramework)` 含 `-ios`），桌面/Android 工程装了也不会有副作用。
装完之后 iOS 上仍要确认宿主是默认的 Metal 合成器（不要强制 `iOSRenderingMode.OpenGl`），
细节见仓库文档[《平台与渲染后端》](../../doc/cn/platform-render-backends.md)。

## 包里有什么

```
native/iossimulator-arm64/libEGL.framework      模拟器切片
native/iossimulator-arm64/libGLESv2.framework
native/ios-arm64/libEGL.framework               真机切片
native/ios-arm64/libGLESv2.framework
build/Aura3D.Angle.iOS.targets                  按 $(RuntimeIdentifier) 选切片并注入 NativeReference
buildTransitive/Aura3D.Angle.iOS.targets        同上（传递消费时命中）
LICENSE.angle.txt                               ANGLE 的 BSD-3-Clause 原文
```

两份切片都随包入库（约 24 MB），所以发布不需要 depot_tools 也不需要 Xcode。
切片缺失时构建会直接报错，不会静默出一个"能编译、运行时不出图"的包。

## 发布

本包有独立的发布通道：手动触发 `build-ios-lib.yml`，在表单里输入要发的版本号——作业用
`-p:Version=<输入>` 打包，全局属性优先于本目录 csproj 里写死的 `<Version>`，所以不改仓库文件也能发任意
版本。同一次作业里 pack 出 nupkg、核对包里的版本确实是输入那个号、逐条核对两份切片与
`build/`、`buildTransitive/` 的 targets 都在包里，再用 GitHub OIDC 换临时 API Key 推到 nuget.org
（secret `NUGET_USER` + `environment: production` 的可信发布策略）。`publish` 勾掉就停在 artifact，只验包
不发布。它不再跟 `pack.yml` 的发版列车一起发：`pack.yml` 的产物集合里已经没有这个包，本仓库自身的
restore 也按主包钉的精确区间从 nuget.org 取它。浏览器侧的 `Aura3D.Avalonia.Browser`
同理，走 `build-browser-lib.yml`。

版本号住在两个互不引用的地方：本目录 csproj 的 `<Version>` 是仓库内不带 `-p` 构建时的默认值，
`Directory.Packages.props` 的 `Aura3DAngleIosVersion` 只喂 `Aura3D.Avalonia` 钉的那条精确区间。
切片热修 = 跑 `build-ios-lib.yml` 并输入一个 nuget.org 上还没有的版本号（重复的号会被拒绝，因为
`--skip-duplicate` 只会静默跳过旧包，那种绿没有意义）→ **同一次提交里**把上面两处字面值都补成刚发的号
→ 再跑 `pack.yml` 重发 `Aura3D.Avalonia`。漏改的下场作业逐一区分：两处互相不一致（csproj 一个号、
props 另一个号）意味着仓库里的这份切片和消费方按区间从 nuget.org 拿到的不是同一份，之后任何一次不带
`-p` 的 pack 都会产出错的号，作业在核对步判 error；只漏 props 那处，主包继续钉旧切片，作业 warning。
之所以区间要跟着走：`Aura3D.Avalonia` 钉的是精确区间 `[<版本>]`，
切片与 `Aura3D.Avalonia` 里的 `DllImport` 签名是 ABI 配对，不能让消费方被动升到一个没配套测过的切片上。
也就是说换切片就意味着重发 `Aura3D.Avalonia`。

`native/` 下没有任何切片时 `dotnet pack` 会直接失败（`AngleNativeCheck`），不会发出空壳包；
但只少一份切片时 pack 照样成功，所以那份核对在 `build-ios-lib.yml` 里。

## 重新构建切片

```shell
./build-angle-ios.sh            # 模拟器切片
./build-angle-ios.sh --device   # 追加真机切片
```

脚本会把 framework 放进 `native/`。当前版本对应 ANGLE
`58f8882372e8a4e83da821ac5d16f0323c3fa1af`，gn 参数
`angle_enable_metal=true`、`is_debug=false`、`enable_rust=false`（standalone checkout 即可，
不需要 Chromium 全量 checkout），真机切片额外需要 `ios_enable_code_signing=false`。
切片按 ANGLE 默认的 `ios_deployment_target` 构建，`minos` 为 18.0。

## 验证状态

- 模拟器 arm64：已验证。`Example.iOS` 去掉本地路径引用、只靠这个包出包，
  `otool -L` 可见 `@rpath/libEGL.framework/libEGL`，Base Geometries 与 PBR RenderPipeline 两页正常出图。
- 真机 arm64：**未验证运行时**。没有可用设备；切片构建通过、按 `ios-arm64` RID 注入正确，
  但真机上的实际渲染没有跑过。

## 许可

包内二进制来自 ANGLE，按 ANGLE 自己的 BSD-3-Clause 分发（原文见 `LICENSE.angle.txt`）。
