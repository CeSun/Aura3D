namespace Aura3D.Core.Resources;

/// <summary>
/// 材质类，定义物体的表面属性和渲染行为
/// </summary>
public class Material : IClone<Material>
{
    /// <summary>
    /// 材质通道列表
    /// </summary>
    public List<Channel> Channels { get; } = [];

    private Dictionary<string, object> parameters { get; set; } = new();

    /// <summary>
    /// 参数字典（只读）
    /// </summary>
    public IReadOnlyDictionary<string, object> Parameters => parameters;

    /// <summary>
    /// 混合模式
    /// </summary>
    public BlendMode BlendMode { get; set; } = BlendMode.Opaque;

    /// <summary>
    /// 是否双面渲染
    /// </summary>
    public bool DoubleSided { get; set; }

    /// <summary>
    /// 透明度阈值
    /// </summary>
    public float AlphaCutoff { get; set; } = 0.5f;

    /// <summary>
    /// 是否有自定义着色器
    /// </summary>
    public bool HasShader => _vertexShaders.Count > 0 || _fragmentShaders.Count > 0;

    /// <summary>
    /// 顶点着色器字典（只读）
    /// </summary>
    public IReadOnlyDictionary<string, string> VertexShaders => _vertexShaders;

    private Dictionary<string, string> _vertexShaders = new();

    private Dictionary<string, string> _fragmentShaders = new();

    /// <summary>
    /// 片段着色器字典（只读）
    /// </summary>
    public IReadOnlyDictionary<string, string> FragmentShaders => _fragmentShaders;

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

        value = default!;
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
        if (value != null)
        {
            parameters[key] = value;
        }
    }

    /// <summary>
    /// 删除参数值
    /// </summary>
    /// <param name="key">参数键名</param>
    public void RemoveParameterValue(string key)
    {
        parameters.Remove(key);
    }

    public Material Clone()
    {
        var m = new Material
        {
            BlendMode = BlendMode,
            DoubleSided = DoubleSided,
            AlphaCutoff = AlphaCutoff,
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

        foreach (var kv in parameters)
            m.parameters[kv.Key] = kv.Value;

        return m;
    }

    public Material DeepClone() => DeepClone(deepCopyTextures: false);

    public Material DeepClone(bool deepCopyTextures)
    {
        var material = new Material
        {
            BlendMode = BlendMode,
            DoubleSided = DoubleSided,
            AlphaCutoff = AlphaCutoff,
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
        material.parameters = new Dictionary<string, object>(parameters);

        return material;
    }

    public void SetChannels(IEnumerable<Channel> channels)
    {
        Channels.Clear();

        foreach (var channel in channels)
        {
            Channels.Add(channel);
        }
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
    }

    public (string? vertexShader, string? fragmentShader) GetShaderSource(string key)
    {
        _vertexShaders.TryGetValue(key, out var vertexShader);
        _fragmentShaders.TryGetValue(key, out var fragmentShader);

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
    }

    public void SetTexture(string name, Texture? texture)
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

    public Texture? GetTexture(string name)
    {
        var channel = Channels.FirstOrDefault(c => c.Name == name);
        if (channel != null)
        {
            return channel.Texture;
        }

        return null;
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

    public Texture? Texture { get; set; }
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
