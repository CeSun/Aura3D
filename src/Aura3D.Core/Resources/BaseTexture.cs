using System.Runtime.InteropServices;

namespace Aura3D.Core.Resources;

/// <summary>
/// Represents the base texture type.
/// </summary>
public abstract class BaseTexture<T> : IVersionedResource where T : BaseTexture<T>
{
    private ulong _localVersion = 1;

    /// <summary>
    /// Gets the version.
    /// </summary>
    public virtual ulong Version
    {
        get => _localVersion;
        protected set => _localVersion = value;
    }

    /// <summary>
    /// Marks the modified.
    /// </summary>
    protected void MarkModified()
    {
        _localVersion++;
    }

    /// <summary>
    /// Gets the width.
    /// </summary>
    private uint _width;
    /// <summary>
    /// Gets the width.
    /// </summary>
    public virtual uint Width
    {
        get => _width;
        set
        {
            if (_width == value)
                return;
            _width = value;
            MarkModified();
        }
    }

    /// <summary>
    /// Gets the height.
    /// </summary>
    private uint _height;
    /// <summary>
    /// Gets the height.
    /// </summary>
    public virtual uint Height
    {
        get => _height;
        set
        {
            if (_height == value)
                return;
            _height = value;
            MarkModified();
        }
    }

    /// <summary>
    /// Performs the convert hdr data to bytes operation.
    /// </summary>
    protected static List<byte> ConvertHdrDataToBytes(ReadOnlySpan<float> data)
    {
        if (data.IsEmpty)
            return [];

        return new List<byte>(MemoryMarshal.AsBytes(data).ToArray());
    }

    /// <summary>
    /// Gets the is hdr.
    /// </summary>
    private bool _isHdr;
    /// <summary>
    /// Gets a value indicating whether the object is hdr.
    /// </summary>
    public virtual bool IsHdr
    {
        get => _isHdr;
        set
        {
            if (_isHdr == value)
                return;
            _isHdr = value;
            MarkModified();
        }
    }
    /// <summary>
    /// Gets the wrap s.
    /// </summary>
    private TextureWrapMode _wrapS = TextureWrapMode.ClampToEdge;
    /// <summary>
    /// Gets the wrap s.
    /// </summary>
    public TextureWrapMode WrapS
    {
        get => _wrapS;
        set
        {
            if (_wrapS == value)
                return;
            _wrapS = value;
            MarkModified();
        }
    }

    /// <summary>
    /// Gets the wrap t.
    /// </summary>
    private TextureWrapMode _wrapT = TextureWrapMode.ClampToEdge;
    /// <summary>
    /// Gets the wrap t.
    /// </summary>
    public TextureWrapMode WrapT
    {
        get => _wrapT;
        set
        {
            if (_wrapT == value)
                return;
            _wrapT = value;
            MarkModified();
        }
    }

    /// <summary>
    /// Gets the min filter.
    /// </summary>
    private TextureFilterMode _minFilter = TextureFilterMode.Linear;
    /// <summary>
    /// Gets the min filter.
    /// </summary>
    public TextureFilterMode MinFilter
    {
        get => _minFilter;
        set
        {
            if (_minFilter == value)
                return;
            _minFilter = value;
            MarkModified();
        }
    }

    /// <summary>
    /// Gets the mag filter.
    /// </summary>
    private TextureFilterMode _magFilter = TextureFilterMode.Linear;
    /// <summary>
    /// Gets the mag filter.
    /// </summary>
    public TextureFilterMode MagFilter
    {
        get => _magFilter;
        set
        {
            if (_magFilter == value)
                return;
            _magFilter = value;
            MarkModified();
        }
    }
    /// <summary>
    /// Gets the color format.
    /// </summary>
    private ColorFormat _colorFormat;
    /// <summary>
    /// Gets the color format.
    /// </summary>
    public ColorFormat ColorFormat
    {
        get => _colorFormat;
        set
        {
            if (_colorFormat == value)
                return;
            _colorFormat = value;
            MarkModified();
        }
    }

    /// <summary>
    /// Gets the is gamma space.
    /// </summary>
    private bool _isGammaSpace;
    /// <summary>
    /// Gets a value indicating whether the object is gamma space.
    /// </summary>
    public bool IsGammaSpace
    {
        get => _isGammaSpace;
        set
        {
            if (_isGammaSpace == value)
                return;
            _isGammaSpace = value;
            MarkModified();
        }
    }


    /// <summary>
    /// Sets the wrap s.
    /// </summary>
    public T SetWrapS(TextureWrapMode mode)
    {
        WrapS = mode;
        return (T)this;
    }

    /// <summary>
    /// Sets the wrap t.
    /// </summary>
    public T SetWrapT(TextureWrapMode mode)
    {
        WrapT = mode;
        return (T)this;
    }

    /// <summary>
    /// Sets the min filter.
    /// </summary>
    public T SetMinFilter(TextureFilterMode mode)
    {
        MinFilter = mode;
        return (T)this;
    }

    /// <summary>
    /// Sets the mag filter.
    /// </summary>
    public T SetMagFilter(TextureFilterMode mode)
    {
        MagFilter = mode;
        return (T)this;
    }

    /// <summary>
    /// Sets the color format.
    /// </summary>
    public T SetColorFormat(ColorFormat format)
    {
        ColorFormat = format;
        return (T)this;
    }


    /// <summary>
    /// Sets the is gamma space.
    /// </summary>
    public T SetIsGammaSpace(bool isGamma)
    {
        IsGammaSpace = isGamma;
        return (T)this;
    }
}
