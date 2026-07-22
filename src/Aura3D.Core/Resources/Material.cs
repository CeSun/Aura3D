namespace Aura3D.Core.Resources;

/// <summary>
/// Represents the material type.
/// </summary>
public class Material : IClone<Material>, IVersionedResource
{
    private readonly List<Channel> _channels = [];

    /// <summary>
    /// Gets or sets the version.
    /// </summary>
    public ulong Version { get; protected set; } = 1;

    private void MarkModified()
    {
        Version++;
    }

    /// <summary>
    /// Gets the channels.
    /// </summary>
    public IReadOnlyList<Channel> Channels => _channels.AsReadOnly();

    private Dictionary<string, object> parameters { get; set; } = new();

    /// <summary>
    /// Gets the parameters.
    /// </summary>
    public IReadOnlyDictionary<string, object> Parameters => parameters;

    /// <summary>
    /// Gets the blend mode.
    /// </summary>
    private BlendMode _blendMode = BlendMode.Opaque;
    /// <summary>
    /// Gets the blend mode.
    /// </summary>
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
    /// Gets the double sided.
    /// </summary>
    private bool _doubleSided;
    /// <summary>
    /// Gets the double sided.
    /// </summary>
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
    /// Gets the alpha cutoff.
    /// </summary>
    private float _alphaCutoff = 0.5f;
    /// <summary>
    /// Gets the alpha cutoff.
    /// </summary>
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
    /// Gets a value indicating whether the object has shader.
    /// </summary>
    public bool HasShader => _vertexShaders.Count > 0 || _fragmentShaders.Count > 0;

    /// <summary>
    /// Gets the vertex shaders.
    /// </summary>
    public IReadOnlyDictionary<string, string> VertexShaders => _vertexShaders;

    private Dictionary<string, string> _vertexShaders = new();

    private Dictionary<string, string> _fragmentShaders = new();

    /// <summary>
    /// Gets the fragment shaders.
    /// </summary>
    public IReadOnlyDictionary<string, string> FragmentShaders => _fragmentShaders;

    /// <summary>
    /// Performs the enumerate parameters operation.
    /// </summary>
    public IEnumerable<KeyValuePair<string, object>> EnumerateParameters()
    {
        foreach (var kv in parameters)
            yield return kv;
    }

    /// <summary>
    /// Attempts to get the parameter value.
    /// </summary>
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
    /// Sets the parameter value.
    /// </summary>
    public void SetParameterValue<T>(string key, T value)
    {
        if (value != null)
        {
            parameters[key] = value;
            MarkModified();
        }
    }

    /// <summary>
    /// Removes the parameter value.
    /// </summary>
    public void RemoveParameterValue(string key)
    {
        if (parameters.Remove(key))
            MarkModified();
    }

    /// <summary>
    /// Clones the associated data.
    /// </summary>
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

    /// <summary>
    /// Deep-clones the associated data.
    /// </summary>
    public Material DeepClone() => DeepClone(deepCopyTextures: false);

    /// <summary>
    /// Deep-clones the associated data.
    /// </summary>
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

    /// <summary>
    /// Sets the channels.
    /// </summary>
    public void SetChannels(IEnumerable<Channel> channels)
    {
        _channels.Clear();

        foreach (var channel in channels)
        {
            _channels.Add(channel);
        }
        MarkModified();
    }

    /// <summary>
    /// Sets the channel.
    /// </summary>
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

    /// <summary>
    /// Sets the shader source.
    /// </summary>
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

    /// <summary>
    /// Gets the shader source.
    /// </summary>
    public (string? vertexShader, string? fragmentShader) GetShaderSource(string key)
    {
        _vertexShaders.TryGetValue(key, out var vertexShader);
        _fragmentShaders.TryGetValue(key, out var fragmentShader);

        return (vertexShader, fragmentShader);
    }

    /// <summary>
    /// Removes the shader.
    /// </summary>
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

    /// <summary>
    /// Sets the texture.
    /// </summary>
    public void SetTexture(string name, Texture? texture)
    {
        SetChannel(new Channel { Name = name, Texture = texture });
    }

    /// <summary>
    /// Gets the texture.
    /// </summary>
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
/// Represents the channel type.
/// </summary>

/// <summary>
/// Represents the channel type.
/// </summary>
public class Channel
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the texture.
    /// </summary>
    public Texture? Texture { get; init; }
}

/// <summary>
/// Specifies values for blend mode.
/// </summary>
public enum BlendMode
{
    /// <summary>
    /// Specifies opaque.
    /// </summary>
    Opaque,
    /// <summary>
    /// Specifies masked.
    /// </summary>
    Masked,
    /// <summary>
    /// Specifies translucent.
    /// </summary>
    Translucent,
}

/// <summary>
/// Specifies values for shader type.
/// </summary>
public enum ShaderType
{
    /// <summary>
    /// Specifies vertex.
    /// </summary>
    Vertex,
    /// <summary>
    /// Specifies fragment.
    /// </summary>
    Fragment,
}
