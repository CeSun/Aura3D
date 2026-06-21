namespace Aura3D.Core.Resources;

/// <summary>
/// 材质类，定义物体的表面属性和渲染行为
/// </summary>
public class Material : IClone<Material>, IVersionedResource
{
    private readonly List<Channel> _channels = [];

    public ulong Version { get; protected set; } = 1;

    private void MarkModified()
    {
        Version++;
    }

    /// <summary>
    /// 材质通道列表
    /// </summary>
    public IReadOnlyList<Channel> Channels => _channels.AsReadOnly();

    private Dictionary<string, object> parameters { get; set; } = new();

    /// <summary>
    /// 参数字典（只读）
    /// </summary>
    public IReadOnlyDictionary<string, object> Parameters => parameters;

    /// <summary>
    /// 混合模式
    /// </summary>
    private BlendMode _blendMode = BlendMode.Opaque;
    public BlendMode BlendMode
    {
        get => _blendMode;
        set
        {
            if (_blendMode == value)
                return;
            _blendMode = value;
            MarkModified();
        }
    }

    /// <summary>
    /// 是否双面渲染
    /// </summary>
    private bool _doubleSided;
    public bool DoubleSided
    {
        get => _doubleSided;
        set
        {
            if (_doubleSided == value)
                return;
            _doubleSided = value;
            MarkModified();
        }
    }

    /// <summary>
    /// 透明度阈值
    /// </summary>
    private float _alphaCutoff = 0.5f;
    public float AlphaCutoff
    {
        get => _alphaCutoff;
        set
        {
            if (_alphaCutoff == value)
                return;
            _alphaCutoff = value;
            MarkModified();
        }
    }

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
            MarkModified();
        }
    }

    /// <summary>
    /// 删除参数值
    /// </summary>
    /// <param name="key">参数键名</param>
    public void RemoveParameterValue(string key)
    {
        if (parameters.Remove(key))
            MarkModified();
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
            m._channels.Add(new Channel
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
            material._channels.Add(new Channel
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
        _channels.Clear();

        foreach (var channel in channels)
        {
            _channels.Add(channel);
        }
        MarkModified();
    }

    public void SetChannel(Channel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);

        var index = _channels.FindIndex(c => c.Name == channel.Name);
        if (index >= 0)
        {
            if (_channels[index].Texture == channel.Texture)
                return;

            _channels[index] = channel;
        }
        else
        {
            _channels.Add(channel);
        }

        MarkModified();
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
        MarkModified();
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
            if (_fragmentShaders.Remove(key))
                MarkModified();
        }
        else if (shaderType == ShaderType.Vertex)
        {
            if (_vertexShaders.Remove(key))
                MarkModified();
        }
    }

    public void SetTexture(string name, Texture? texture)
    {
        SetChannel(new Channel { Name = name, Texture = texture });
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
    public string Name { get; init; } = string.Empty;

    public Texture? Texture { get; init; }
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
