using Aura3D.Core.Resources;
using Texture = Aura3D.Core.Resources.Texture;

namespace Aura3D.Examples.Kit;

/// <summary>
/// 演示页内嵌的着色器片段。自定义材质通过
/// <see cref="Material.SetShaderSource(string,Aura3D.Core.Resources.ShaderType,string)"/> 按
/// pass 的类名（<c>LightPass</c> / <c>NoLightPass</c> / <c>PointCloudPass</c>）挂 GLSL ES 覆盖，
/// 所以这些片段是共享工程里唯一直接写 GLSL 的地方。
/// <para>
/// 引擎固定提供的 uniform 名为 <c>modelMatrix</c>、<c>viewMatrix</c>、<c>projectionMatrix</c>、
/// <c>cameraPosition</c>，材质参数（<c>SetParameterValue</c>）按同名 uniform 绑定；
/// 顶点属性固定槽位是 0 位置、1 UV、2 顶点色、3 法线。
/// </para>
/// </summary>
public static class Shaders
{
    /// <summary><c>LightPass</c>（Blinn-Phong 主 pass）的常量色顶点着色器。</summary>
    public const string IdentityVertex = """
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
        """;

    /// <summary>常量色片元，颜色来自材质参数 <c>uColor</c>。</summary>
    public const string SolidFragment = """
        #version 300 es
        precision highp float;

        uniform vec4 uColor;

        out vec4 outColor;

        void main()
        {
            outColor = uColor;
        }
        """;

    /// <summary>带 UV 与顶点色的直通顶点着色器，给 <c>NoLightPass</c> 用。</summary>
    public const string TexturedVertex = """
        #version 300 es
        precision highp float;

        layout(location = 0) in vec3 position;
        layout(location = 1) in vec2 texCoord;

        uniform mat4 modelMatrix;
        uniform mat4 viewMatrix;
        uniform mat4 projectionMatrix;

        out vec2 vTexCoord;

        void main()
        {
            vTexCoord = texCoord;
            gl_Position = projectionMatrix * viewMatrix * modelMatrix * vec4(position, 1.0);
        }
        """;

    /// <summary>
    /// 贴图采样后乘以材质参数 <c>uColor</c> 作为染色。
    /// </summary>
    public const string TexturedFragment = """
        #version 300 es
        precision highp float;

        in vec2 vTexCoord;

        uniform sampler2D BaseColorTexture;
        uniform vec4 uColor;

        out vec4 outColor;

        void main()
        {
            outColor = texture(BaseColorTexture, vTexCoord) * uColor;
        }
        """;

    /// <summary>
    /// 顶点色直出的顶点着色器。引擎固定槽位：0 位置、1 UV、2 顶点色（vec4）、3 法线。
    /// </summary>
    public const string VertexColorVertex = """
        #version 300 es
        precision highp float;

        layout(location = 0) in vec3 position;
        layout(location = 2) in vec4 color;

        uniform mat4 modelMatrix;
        uniform mat4 viewMatrix;
        uniform mat4 projectionMatrix;

        out vec4 vColor;

        void main()
        {
            vColor = color;
            gl_Position = projectionMatrix * viewMatrix * modelMatrix * vec4(position, 1.0);
        }
        """;

    /// <summary>顶点色乘以材质参数 <c>uColor</c> 的片元着色器。</summary>
    public const string VertexColorFragment = """
        #version 300 es
        precision highp float;

        in vec4 vColor;

        uniform vec4 uColor;

        out vec4 outColor;

        void main()
        {
            outColor = vColor * uColor;
        }
        """;

    /// <summary>
    /// 点图元顶点着色器：点尺寸来自材质参数 <c>uPointSize</c>。
    /// WebGL2/GLES3 对 <c>gl_PointSize</c> 的上限由各设备决定，演示里限制在 64 以内。
    /// </summary>
    public const string PointVertex = """
        #version 300 es
        precision highp float;

        layout(location = 0) in vec3 position;
        layout(location = 2) in vec4 color;

        uniform mat4 modelMatrix;
        uniform mat4 viewMatrix;
        uniform mat4 projectionMatrix;
        uniform float uPointSize;

        out vec4 vColor;

        void main()
        {
            vColor = color;
            gl_Position = projectionMatrix * viewMatrix * modelMatrix * vec4(position, 1.0);
            gl_PointSize = uPointSize;
        }
        """;

    /// <summary>把方形点精灵裁成圆片的片元着色器，用 <c>gl_PointCoord</c> 定位。</summary>
    public const string PointFragment = """
        #version 300 es
        precision highp float;

        in vec4 vColor;

        uniform vec4 uColor;

        out vec4 outColor;

        void main()
        {
            float dist = length(gl_PointCoord - vec2(0.5));

            if (dist > 0.5)
                discard;

            outColor = vColor * uColor * (1.0 - dist * 0.6);
        }
        """;

    /// <summary>
    /// 建一个常量色材质，替换指定 pass 的着色器。
    /// </summary>
    /// <param name="passShaderName">要覆盖的 pass 类名，如 <c>LightPass</c>。</param>
    /// <param name="color">直出颜色。</param>
    public static Material Solid(string passShaderName, System.Numerics.Vector4 color)
    {
        var material = new Material { BlendMode = BlendMode.Opaque };

        material.SetShaderSource(passShaderName, Core.Resources.ShaderType.Vertex, IdentityVertex);
        material.SetShaderSource(passShaderName, Core.Resources.ShaderType.Fragment, SolidFragment);
        material.SetParameterValue("uColor", color);

        return material;
    }

    /// <summary>
    /// 建一个只吃顶点色（槽位 2）的材质，用于没有法线与 UV 的手写几何体。
    /// </summary>
    /// <param name="passShaderName">要覆盖的 pass 类名。</param>
    /// <param name="tint">整体染色，缺省不做修改。</param>
    public static Material VertexColors(string passShaderName, System.Numerics.Vector4? tint = null)
    {
        var material = new Material { BlendMode = BlendMode.Opaque };

        material.SetShaderSource(passShaderName, Core.Resources.ShaderType.Vertex, VertexColorVertex);
        material.SetShaderSource(passShaderName, Core.Resources.ShaderType.Fragment, VertexColorFragment);
        material.SetParameterValue("uColor", tint ?? new System.Numerics.Vector4(1));

        return material;
    }

    /// <summary>
    /// 建一个点图元材质：顶点色 + 圆形点精灵 + 可调点尺寸。
    /// </summary>
    /// <param name="passShaderName">要覆盖的 pass 类名。</param>
    /// <param name="pointSize">初的点尺寸（像素）。</param>
    public static Material Points(string passShaderName, float pointSize)
    {
        var material = new Material { BlendMode = BlendMode.Translucent };

        material.SetShaderSource(passShaderName, Core.Resources.ShaderType.Vertex, PointVertex);
        material.SetShaderSource(passShaderName, Core.Resources.ShaderType.Fragment, PointFragment);
        material.SetParameterValue("uColor", new System.Numerics.Vector4(1));
        material.SetParameterValue("uPointSize", pointSize);

        return material;
    }

    /// <summary>
    /// 建一个贴图为主的材质（通道名 <c>BaseColor</c> → sampler <c>BaseColorTexture</c>）。
    /// </summary>
    public static Material Textured(
        string passShaderName,
        Texture texture,
        System.Numerics.Vector4? tint = null)
    {
        var material = new Material { BlendMode = BlendMode.Opaque };

        material.SetShaderSource(passShaderName, Core.Resources.ShaderType.Vertex, TexturedVertex);
        material.SetShaderSource(passShaderName, Core.Resources.ShaderType.Fragment, TexturedFragment);
        material.SetTexture("BaseColor", texture);
        material.SetParameterValue("uColor", tint ?? new System.Numerics.Vector4(1));

        return material;
    }

    /// <summary>
    /// 扫光条纹片元着色器。三个自定义参数演示按名绑定的三种类型
    /// （<c>vec4 uColor</c>、<c>vec2 uStripe</c>、<c>float uTime</c>），
    /// <c>//{{defines}}</c> 是引擎写入 <c>#define</c> 的注入点——
    /// 混合模式由 pass 以 <c>BLENDMODE_MASKED</c> / <c>BLENDMODE_TRANSLUCENT</c> 传进来，
    /// 所以同一段代码在三种混合模式下走不同分支，<c>alphaCutoff</c> 也是引擎固定绑定的 uniform。
    /// </summary>
    public const string StripeFragment = """
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
        """;

    /// <summary>
    /// 建一个扫光条纹材质，只覆盖片元着色器，顶点沿用该 pass 的默认实现（需要 UV）。
    /// </summary>
    /// <param name="passShaderName">要覆盖的 pass 类名。</param>
    /// <param name="color">底色与不透明度。</param>
    /// <param name="stripe">条纹在 UV 两个方向上的重复数。</param>
    public static Material Stripes(string passShaderName, System.Numerics.Vector4 color, System.Numerics.Vector2 stripe)
    {
        var material = new Material { BlendMode = BlendMode.Opaque };

        material.SetShaderSource(passShaderName, Core.Resources.ShaderType.Fragment, StripeFragment);
        material.SetParameterValue("uColor", color);
        material.SetParameterValue("uStripe", stripe);
        material.SetParameterValue("uTime", 0f);

        return material;
    }

    /// <summary>
    /// 实例化顶点着色器：读顶点色（槽 2）与逐实例矩阵（槽 8–11）。
    /// <para>
    /// 覆盖顶点阶段就等于接管了实例变换——引擎不会替你把 <c>INSTANCED_MESH</c> 的
    /// 槽位 8–11 乘进去，所以必须像这里一样按 <c>//{{defines}}</c> 传进来的
    /// <c>INSTANCED_MESH</c> 分支声明属性。非实例路径下同名 uniform 由 pass 绑定，
    /// 因此一个材质能同时用在 <c>Mesh</c> 与 <c>InstancedMesh</c> 上。
    /// </para>
    /// </summary>
    public const string InstancedColorVertex = """
        #version 300 es
        precision highp float;
        //{{defines}}

        layout(location = 0) in vec3 position;
        layout(location = 2) in vec4 color;

        #ifdef INSTANCED_MESH
        layout(location = 8) in mat4 modelMatrix;
        #else
        uniform mat4 modelMatrix;
        #endif

        uniform mat4 viewMatrix;
        uniform mat4 projectionMatrix;

        out vec4 vColor;

        void main()
        {
            vColor = color;
            gl_Position = projectionMatrix * viewMatrix * modelMatrix * vec4(position, 1.0);
        }
        """;

    /// <summary>
    /// 建一个逐实例顶点色材质：顶点用 <see cref="InstancedColorVertex"/>，片元用
    /// <see cref="VertexColorFragment"/>，配合
    /// <c>InstancedMesh.SetInstanceAttribute(BuildInVertexAttribute.Color_0, 4, …)</c> 使用。
    /// </summary>
    /// <param name="passShaderName">要覆盖的 pass 类名。</param>
    /// <param name="tint">整体染色，缺省不做修改。</param>
    public static Material InstanceColors(string passShaderName, System.Numerics.Vector4? tint = null)
    {
        var material = new Material { BlendMode = BlendMode.Opaque };

        material.SetShaderSource(passShaderName, Core.Resources.ShaderType.Vertex, InstancedColorVertex);
        material.SetShaderSource(passShaderName, Core.Resources.ShaderType.Fragment, VertexColorFragment);
        material.SetParameterValue("uColor", tint ?? new System.Numerics.Vector4(1));

        return material;
    }
}
