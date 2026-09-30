---
section: advanced
order: 2
---

# 自定义材质与着色器

这一篇解决一件事：**只想改某个材质的着色行为，而不必从头写一整条渲染管线。** 有两种典型场景——给某个材质换掉它在一个 Pass 里的 GLSL（常量色、扫光、只换片元都行），以及给实例化网格写一段能读「逐实例属性」的着色器。真要接管整条管线，那是[自定义渲染管线](./custom-pipeline.md)的事。

## 最短可跑：把某个材质换成常量色

`SetShaderSource(passKey, ShaderType, src)` 按 Pass 名字给材质挂上自定义 GLSL，覆盖该 Pass 的顶点 / 片元着色器。下面这段用一个常量色替换掉不透明物体的光照，颜色来自材质参数 `uColor`：

```csharp
var material = new Material { BlendMode = BlendMode.Opaque };

material.SetShaderSource("LightPass", ShaderType.Vertex, """
    #version 300 es
    precision highp float;

    layout(location = 0) in vec3 position;

    uniform mat4 modelMatrix;
    uniform mat4 viewMatrix;
    uniform mat4 projectionMatrix;

    void main()
    {
        gl_Position = projectionMatrix * viewMatrix * modelMatrix * vec4(position, 1.0);
    }
    """);

material.SetShaderSource("LightPass", ShaderType.Fragment, """
    #version 300 es
    precision highp float;

    uniform vec4 uColor;

    out vec4 outColor;

    void main()
    {
        outColor = uColor;
    }
    """);

// uColor 与片元里的同名 uniform 绑定
material.SetParameterValue("uColor", new Vector4(1f, 0f, 0f, 1f));

var mesh = new Mesh { Geometry = new BoxGeometry(), Material = material };
view.AddNode(mesh);
```

## passKey：填 Pass 的名字

`passKey` 就是渲染 Pass 的名字（等于 Pass 类名）。内置 Blinn-Phong 主管线里，给**不透明 / 镂空**物体打光的 Pass 叫 `LightPass`；**半透明**物体走的是另一个 Pass `TranslucentPass`。

> [!WARNING]
> 你只覆盖 `LightPass`、却把材质切成 `BlendMode.Translucent`，就会看不到自己的着色器——那物体此刻走的是 `TranslucentPass`。要让自定义着色器在半透明物体上生效，得对 `"TranslucentPass"` 这个 key 再挂一遍。

顶点、片元两套可以分别覆盖，也能**只覆盖一套**——没覆盖的那套沿用该 Pass 的默认源码。下面的扫光效果就只换片元、顶点仍用引擎默认（因此模型得带 UV）：

```csharp
var material = new Material { BlendMode = BlendMode.Opaque };

material.SetShaderSource("LightPass", ShaderType.Fragment, """
    #version 300 es
    precision highp float;
    //{{defines}}

    in vec2 vTexCoord;

    uniform vec4 uColor;
    uniform vec2 uStripe;
    uniform float uTime;
    uniform float alphaCutoff;

    out vec4 outColor;

    void main()
    {
        float phase = vTexCoord.x * uStripe.x + vTexCoord.y * uStripe.y + uTime;
        float edge = smoothstep(0.35, 0.95, sin(phase * 6.28318));

        float alpha = uColor.a;

    #if defined(BLENDMODE_MASKED) || defined(BLENDMODE_TRANSLUCENT)
        alpha = mix(0.0, uColor.a, edge);

        if (alpha <= alphaCutoff)
            discard;
    #endif

        outColor = vec4(mix(uColor.rgb, vec3(1.0), edge), alpha);
    }
    """);

material.SetParameterValue("uColor",  new Vector4(0.15f, 0.45f, 0.85f, 1f));
material.SetParameterValue("uStripe", new Vector2(6f, 2f));
material.SetParameterValue("uTime",   0f);

// 每帧推 uTime，让条纹流动起来
material.SetParameterValue("uTime", (material.TryGetParameterValue("uTime", out float t) ? t : 0f) + (float)deltaTime);
```

## 引擎会固定给你的东西

写这些 GLSL 时，引擎按约定自动喂给你：

- **uniform**：`modelMatrix`、`viewMatrix`、`projectionMatrix`、`cameraPosition` 总是存在；`alphaCutoff` 取材质上的 `AlphaCutoff`。
- **通道 → sampler**：材质通道名 `<X>` 会被绑成名为 `<X>Texture` 的 `sampler2D`——通道 `BaseColor` 就是片元里的 `BaseColorTexture`（自定义 GLSL 需要自己声明并采样它）。
- **顶点属性固定槽位**：0=位置、1=UV、2=顶点色（`vec4`）、3=法线。
- **材质参数 → uniform**：`SetParameterValue(name, value)` 把值绑到**同名** uniform；值的类型决定是 `float` / `Vector2/3/4` / `Matrix4x4` / `int` 中的哪一种。
- **宏注入点**：GLSL 里留一行 `//{{defines}}`，编译时引擎把当前宏替换进去。混合模式由 Pass 传成 `BLENDMODE_MASKED` / `BLENDMODE_TRANSLUCENT`，实例化传 `INSTANCED_MESH`、蒙皮传 `SKINNED_MESH`——所以同一段片元能用 `#if defined(BLENDMODE_MASKED)` 分走不同分支（像上面那样只在镂空 / 半透明时 `discard`）。

> [!NOTE]
> 材质级的自定义源码优先于 Pass 的默认源码；同一个「Pass 名 + 宏组合」只编译一次，后续帧直接复用缓存。宏的顺序会影响缓存键，别一会儿 `A,B` 一会儿 `B,A`。

## 逐实例自定义属性着色器

要给每个实例传一份自定义数据（最常用是颜色），走材质级着色器 + 逐实例顶点属性这条路。约定是：**`TexCoord_1` 落在 `location=16` 用来放 `instanceColor`；`INSTANCED_MESH` 宏下逐实例的 `modelMatrix` 落在 `location=8`**（一个 `mat4` 占 4 个连续槽 8–11，声明写在 8）。实例化本身的用法见[实例化渲染](./instanced-rendering.md)。

C# 侧——给实例网格的材质挂上下面这段 GLSL，并把逐实例颜色喂到 `location=16`：

```csharp
var material = new Material { BlendMode = BlendMode.Opaque };
material.SetShaderSource("LightPass", ShaderType.Vertex, instanceColorVert);
material.SetShaderSource("LightPass", ShaderType.Fragment, instanceColorFrag);

var sourceMesh = new Mesh { Geometry = new BoxGeometry(), Material = material };
var instancedMesh = InstancedMesh.FromMesh(sourceMesh);
instancedMesh.SetAttributeEnabled("InstanceNormalTransform", false);   // 不需要逐实例法线，省带宽

var colors = new List<Vector4>();
for (int i = 0; i < totalInstances; i++)
    colors.Add(new Vector4((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), 1f));

instancedMesh.SetInstanceAttribute<Vector4>(BuildInVertexAttribute.TexCoord_1, 4, colors); // → location 16
view.AddNode(instancedMesh);
```

顶点着色器要点：一旦你**覆盖顶点阶段，就等于接管了实例变换**——引擎不会替你把逐实例矩阵乘进去，所以必须在 `#ifdef INSTANCED_MESH` 分支里按 `location=8` 声明 `mat4 modelMatrix`，并声明 `location=16` 的 `instanceColor`：

```glsl
#version 300 es
precision highp float;
//{{defines}}

layout(location = 0) in vec3 position;

#ifdef INSTANCED_MESH
layout(location = 8) in mat4 modelMatrix;
layout(location = 16) in vec4 instanceColor;
#else
uniform mat4 modelMatrix;
uniform vec4 instanceColor;
#endif

uniform mat4 viewMatrix;
uniform mat4 projectionMatrix;

out vec4 vColor;

void main()
{
    vColor = instanceColor;
    gl_Position = projectionMatrix * viewMatrix * modelMatrix * vec4(position, 1.0);
}
```

片元把传下来的逐实例颜色直出即可：

```glsl
#version 300 es
precision highp float;

in vec4 vColor;

out vec4 outColor;

void main()
{
    outColor = vColor;
}
```

## 常见坑

> [!WARNING]
> **GLSL 源码里不要写中文注释或全角字符**——会让着色器编译失败。注释一律只用 ASCII；要解释就写在 C# 侧。

- `passKey` 对不上实际 Pass：半透明物体走 `TranslucentPass`，只覆盖 `LightPass` 不生效。
- 材质参数没起作用：uniform 名要和 `SetParameterValue` 的键**逐字一致**，拼错了不报错，只是那个 uniform 保持默认值。
- 逐实例颜色全黑 / 全白：`location` 没按约定对齐（`TexCoord_1`→15、`modelMatrix`→7），或忘了 `#ifdef INSTANCED_MESH` 分支自己乘逐实例矩阵。
- `discard` 只在镂空 / 半透明分支用：见上面的 `#if defined(...)`，不透明下乱 discard 会把整个物体裁没了——症状排查见[常见坑与排障](./troubleshooting.md)。
- 每次换宏组合都会重新编译一次着色器；把材质参数尽量放 `SetParameterValue`，别为改个颜色反复 `SetShaderSource`。

## 看看实际效果

Gallery 的 **MaterialShaders** 示例把这三件事演示全了：通道 `SetTexture`、常量色 / 只换片元的自定义着色器、以及材质参数驱动的程序化条纹与镂空 / 半透明对比。

源码：[MaterialShadersDemo.axaml.cs](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/MaterialShaders/MaterialShadersDemo.axaml.cs)、着色器片段在 [Kit/Shaders.cs](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Kit/Shaders.cs)。内置 Pass 名与宏的完整清单见[内置 Pass 与着色器宏速查](./reference-shaders.md)。
