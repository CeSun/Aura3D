<div id="header" align="center">
	<h3>Aura3D</h3>
	<h4><i>一个轻量级、可扩展、高性能的3D渲染控件</i></h4>
	<div id="link" >
		<a href="./README.md">English</a> | 
		<span>中文</span> |
		<a href="./doc/cn/home.md">文档</a> 
	</div>
</div>
<br/>

## 特性
### 1. 基础功能
- 模型渲染
- 光源
- 投影
- 蒙皮动画
- 实例化渲染
- 默认布林冯渲染管线
### 2. 进阶功能
- 自定义管线 
### 3. 支持平台
- Avalonia

## 开始上手

在 Avalonia 项目中添加 Aura3D.Avalonia 包：

```shell
dotnet add package Aura3D.Avalonia
```

然后在你的项目中使用 Aura3DView 控件, 并绑定 SceneInitialized 事件：

```xaml
<Window
    ...
    xmlns:aura3d="clr-namespace:Aura3D.Avalonia;assembly=Aura3D.Avalonia"
    ...>
	<aura3d:Aura3DView x:Name="aura3Dview" SceneInitialized="OnSceneInitialized"/>
</Window>
```

在 SceneInitialized 事件中，初始化你的场景：

```
 public void OnSceneInitialized(object sender, RoutedEventArgs args)
 {

    var view = (Aura3DView)sender;

    var camera = new Camera();

    camera.ClearColor = Color.Gray;

	view.AddNode(camera);

	var model = ModelLoader.LoadGlbModel("your model file path(*.glb)");

	model.Position = camera.Forward * 3;

	view.AddNode(model);

 }
```

