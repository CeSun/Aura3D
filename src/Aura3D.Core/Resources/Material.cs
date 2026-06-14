using Aura3D.Core.Renderers;
using System.Drawing;

namespace Aura3D.Core.Resources;

/// <summary>
/// 材质类，定义物体的表面属性和渲染行为
/// </summary>
public class Material : IClone<Material>, IGpuResource
{
    /// <summary>
    /// 是否需要上传到GPU
    /// </summary>
    public bool NeedsUpload { get; set; } = false;
    /// <summary>
    /// 材质通道列表
    /// </summary>
    public List<Channel> Channels { get; set; } = [];

    private Dictionary<string, object> parameters  { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// 混合模式
    /// </summary>
    public BlendMode BlendMode { get; set; } = BlendMode.Opaque;

    /// <summary>
    /// 是否双面渲染
    /// </summary>
    public bool DoubleSided { get; set; } = false;

    /// <summary>
    /// 透明度阈值
    /// </summary>
    public float AlphaCutoff { get; set; } = 0.5f;

    /// <summary>
    /// 是否有自定义着色器
    /// </summary>
    public bool HasShader { get; set; } = false;
    /// <summary>
    /// 顶点着色器字典（只读）
    /// </summary>
    public IReadOnlyDictionary<string, string> VertexShaders => _vertexShaders;

    private Dictionary<string, string> _vertexShaders = new Dictionary<string, string>();

    private Dictionary<string, string> _fragmentShaders = new Dictionary<string, string>();

    /// <summary>
    /// 片段着色器字典（只读）
    /// </summary>
    public IReadOnlyDictionary<string, string> FragmentShaders => _fragmentShaders;

    /// <summary>
    /// 着色器字典
    /// </summary>
    public Dictionary<string, Shader> Shaders { get; } = new Dictionary<string, Shader>();

    /// <summary>
    /// 尝试获取参数值
    /// </summary>
    /// <typeparam name="T">参数类型</typeparam>
    /// <param name="key">参数键名</param>
    /// <param name="value">参数值</param>
    /// <returns>是否成功获取</returns>
    /// <summary>
    /// 遍历所有参数键值对
    /// </summary>
    public IEnumerable<KeyValuePair<string, object>> EnumerateParameters()
    {
        foreach (var kv in parameters)
            yield return kv;
    }

    public bool TryGetParameterValue<T>(string key, out T value)
    {
        if (parameters.TryGetValue(key, out var obj) && obj is T t)
        {
            value = t;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// 设置参数值
    /// </summary>
    /// <typeparam name="T">参数类型</typeparam>
    /// <param name="key">参数键名</param>
    /// <param name="value">参数值</param>
    public void SetParameterValue<T>(string key, T value)
    {
        if(value != null)
        {
            parameters[key] = value;
        }
    }


    public Material Clone()
    {
        var m = new Material
        {
            BlendMode = this.BlendMode,
            DoubleSided = this.DoubleSided,
            AlphaCutoff = this.AlphaCutoff,
            HasShader = this.HasShader,
        };

        foreach (var channel in Channels)
        {
            m.Channels.Add(new Channel
            {
                Name = channel.Name,
                Texture = channel.Texture
            });
        }

        foreach (var kv in _vertexShaders)
            m._vertexShaders[kv.Key] = kv.Value;
        foreach (var kv in _fragmentShaders)
            m._fragmentShaders[kv.Key] = kv.Value;
        foreach (var kv in ShaderPassParametersCallbacks)
            m.ShaderPassParametersCallbacks[kv.Key] = kv.Value;
        foreach (var kv in parameters)
            m.parameters[kv.Key] = kv.Value;
        return m;
    }

    public Material DeepClone() => DeepClone(deepCopyTextures: false);

    public Material DeepClone(bool deepCopyTextures)
    {
        var material = new Material
        {
            BlendMode = this.BlendMode,
            DoubleSided = this.DoubleSided,
            AlphaCutoff = this.AlphaCutoff,
            HasShader = this.HasShader,
        };

        foreach (var channel in Channels)
        {
            material.Channels.Add(new Channel
            {
                Name = channel.Name,
                Texture = channel.Texture is Texture t
                    ? (deepCopyTextures ? t.DeepClone() : t.Clone())
                    : null
            });
        }

        material._vertexShaders = new Dictionary<string, string>(_vertexShaders);
        material._fragmentShaders = new Dictionary<string, string>(_fragmentShaders);
        material.ShaderPassParametersCallbacks = new Dictionary<string, Action<RenderPass>>(ShaderPassParametersCallbacks);
        material.parameters = new Dictionary<string, object>(parameters);

        return material;
    }

    public Dictionary<string, Action<RenderPass>> ShaderPassParametersCallbacks = [];
    public void SetShaderPassParametersCallback(string key, Action<RenderPass> callback)
    {
        ShaderPassParametersCallbacks[key] = callback;
    }

    public void RemoveShaderPassParametersCallback(string key)
    {
        ShaderPassParametersCallbacks.Remove(key);
    }

    public Action<RenderPass>? GetShaderPassParametersCallback(string key)
    {
        Action<RenderPass>? callback = null;

        ShaderPassParametersCallbacks.TryGetValue(key, out callback);

        return callback;
    }

    public void SetShaderSource(string key, ShaderType shaderType, string shader)
    {
        if (shaderType == ShaderType.Fragment)
        {
            _fragmentShaders[key] = shader;
        }
        else if (shaderType == ShaderType.Vertex)
        {
            _vertexShaders[key] = shader;
        }
        HasShader = true;
    }

    public (string? vertexShader, string? fragmentShader) GetShaderSource(string key)
    {
        string? vertexShader = null;

        string? fragmentShader = null;


        _vertexShaders.TryGetValue(key, out vertexShader);

        _fragmentShaders.TryGetValue(key, out fragmentShader);

       return (vertexShader, fragmentShader);
    }

    public void RemoveShader(string key, ShaderType shaderType)
    {
        if (shaderType == ShaderType.Fragment)
        {
            _fragmentShaders.Remove(key);
        }
        else if (shaderType == ShaderType.Vertex)
        {
            _vertexShaders.Remove(key);
        }
        if (_fragmentShaders.Count == 0 && _vertexShaders.Count == 0)
        {
            HasShader = false;
        }
    }


    public void Upload(Silk.NET.OpenGLES.GL gl)
    {
    }

    public void Destroy(Silk.NET.OpenGLES.GL gl)
    {
        foreach(var shader in Shaders)
        {
            gl.DeleteProgram(shader.Value.ProgramId);
        }
        Shaders.Clear();
    }

    public ITexture? BaseColor
    {
        get => GetTexture("BaseColor");
        set => SetTexture("BaseColor", value);
    }

    public ITexture? Normal
    {
        get => GetTexture("Normal");
        set => SetTexture("Normal", value);
    }

    public void SetTexture(string name, ITexture? texture)
    {
        var channel = Channels.FirstOrDefault(c => c.Name == name);
        if (channel != null)
        {
            channel.Texture = texture;
        }
        else
        {
            Channels.Add(new Channel { Name = name, Texture = texture });
        }
    }

    public ITexture? GetTexture(string name)
    {
        var channel = Channels.FirstOrDefault(c => c.Name == name);
        if (channel != null)
        {
             return channel.Texture;
        }
        else
        {
            return null;
        }
    }


}

/// <summary>
/// 材质通道类，包含纹理通道
/// </summary>

/// <summary>
/// 通道名称
/// </summary>
public class Channel
{
    public string Name { get; set; } = string.Empty;

    public ITexture? Texture { get; set; }
}

public enum BlendMode
{
    Opaque,
    Masked,
    Translucent,
}

public enum ShaderType
{
    Vertex,
    Fragment,
}