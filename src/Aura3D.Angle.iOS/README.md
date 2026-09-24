# Aura3D.Angle.iOS

[Aura3D](https://github.com/CeSun/Aura3d) 的 iOS 渲染路径自带 ANGLE（Metal 后端）上下文，
但 ANGLE 的原生库必须由**应用**链接进主可执行文件：`Aura3D.Avalonia` 里的桥接代码用
`DllImport("__Internal")` 解析符号（为了避开 Apple 自带的 OpenGLES 符号混用），
所以库不能只放在类包里。这个包就是替应用做这件事：装上之后 targets 自动注入
`NativeReference`，不需要写任何配置。

## 用法

```shell
dotnet add package Aura3D.Angle.iOS
```

只作用于 iOS 目标框架（`$(TargetFramework)` 含 `-ios`），桌面/Android 工程装了也不会有副作用。
装完之后 iOS 上仍要确认宿主是默认的 Metal 合成器（不要强制 `iOSRenderingMode.OpenGl`），
细节见仓库文档《平台与渲染后端》。

## 包里有什么

```
native/iossimulator-arm64/libEGL.framework      模拟器切片
native/iossimulator-arm64/libGLESv2.framework
native/ios-arm64/...                            真机切片（有则打包）
build/Aura3D.Angle.iOS.targets                  注入 NativeReference
buildTransitive/Aura3D.Angle.iOS.targets        同上（传递消费时命中）
LICENSE.angle.txt                               ANGLE 的 BSD-3-Clause 原文
```

切片缺失时构建会直接报错，不会静默出一个"能编译、运行时不出图"的包。

## 重新构建切片

```shell
./build-angle-ios.sh            # 模拟器切片
./build-angle-ios.sh --device   # 追加真机切片
dotnet pack -c Release -o artifacts/nupkgs
```

脚本会把 framework 放进 `native/`（已 gitignore，构建产物不入库）。当前版本对应
ANGLE `58f8882372e8a4e83da821ac5d16f0323c3fa1af`，gn 参数
`angle_enable_metal=true`、`is_debug=false`、`enable_rust=false`（standalone checkout 即可，
不需要 Chromium 全量 checkout）。

## 验证状态

- 模拟器 arm64：已验证。`Example.iOS` 去掉本地路径引用、只靠这个包出包，
  `otool -L` 可见 `@rpath/libEGL.framework/libEGL`，Base Geometries 与 PBR RenderPipeline 两页正常出图。
- 真机 arm64：**未验证**。没有可用设备，切片本身能构建但运行时行为未知。

## 许可

包内二进制来自 ANGLE，按 ANGLE 自己的 BSD-3-Clause 分发（原文见 `LICENSE.angle.txt`）。
