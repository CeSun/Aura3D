using System.Runtime.InteropServices;

namespace Aura3D.Core.Resources;

/// <summary>
/// 纹理基类，提供纹理的通用属性和方法
/// </summary>
/// <typeparam name="T">纹理类型</typeparam>
public abstract class BaseTexture<T> : IVersionedResource where T : BaseTexture<T>
{
    public ulong Version { get; protected set; } = 1;

    protected void MarkModified()
    {
        Version++;
    }

    /// <summary>
    /// 纹理宽度
    /// </summary>
    private uint _width;
    public uint Width
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
    /// 纹理高度
    /// </summary>
    private uint _height;
    public uint Height
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
    /// 将 HDR 浮点数据转换为字节缓冲（按 IEEE 浮点字节序原样存储）。
    /// </summary>
    /// <param name="data">HDR 浮点数据。</param>
    /// <returns>字节缓冲列表；空数据返回空列表。</returns>
    protected static List<byte> ConvertHdrDataToBytes(ReadOnlySpan<float> data)
    {
        if (data.IsEmpty)
            return [];

        return new List<byte>(MemoryMarshal.AsBytes(data).ToArray());
    }

    /// <summary>
    /// 是否为 HDR 纹理
    /// </summary>
    private bool _isHdr;
    public bool IsHdr
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
    /// S 方向环绕模式
    /// </summary>
    private TextureWrapMode _wrapS = TextureWrapMode.ClampToEdge;
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
    /// T 方向环绕模式
    /// </summary>
    private TextureWrapMode _wrapT = TextureWrapMode.ClampToEdge;
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
    /// 缩小过滤模式
    /// </summary>
    private TextureFilterMode _minFilter = TextureFilterMode.Linear;
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
    /// 放大过滤模式
    /// </summary>
    private TextureFilterMode _magFilter = TextureFilterMode.Linear;
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
    /// 颜色格式
    /// </summary>
    private ColorFormat _colorFormat;
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
    /// 是否在伽马空间
    /// </summary>
    private bool _isGammaSpace;
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
    /// 设置 S 方向环绕模式
    /// </summary>
    /// <param name="mode">环绕模式</param>
    /// <returns>当前纹理对象</returns>
    public T SetWrapS(TextureWrapMode mode)
    {
        WrapS = mode;
        return (T)this;
    }

    /// <summary>
    /// 设置 T 方向环绕模式
    /// </summary>
    /// <param name="mode">环绕模式</param>
    /// <returns>当前纹理对象</returns>
    public T SetWrapT(TextureWrapMode mode)
    {
        WrapT = mode;
        return (T)this;
    }

    /// <summary>
    /// 设置缩小过滤模式
    /// </summary>
    /// <param name="mode">过滤模式</param>
    /// <returns>当前纹理对象</returns>
    public T SetMinFilter(TextureFilterMode mode)
    {
        MinFilter = mode;
        return (T)this;
    }

    /// <summary>
    /// 设置放大过滤模式
    /// </summary>
    /// <param name="mode">过滤模式</param>
    /// <returns>当前纹理对象</returns>
    public T SetMagFilter(TextureFilterMode mode)
    {
        MagFilter = mode;
        return (T)this;
    }

    /// <summary>
    /// 设置颜色格式
    /// </summary>
    /// <param name="format">颜色格式</param>
    /// <returns>当前纹理对象</returns>
    public T SetColorFormat(ColorFormat format)
    {
        ColorFormat = format;
        return (T)this;
    }


    /// <summary>
    /// 设置是否在伽马空间
    /// </summary>
    /// <param name="isGamma">是否在伽马空间</param>
    /// <returns>当前纹理对象</returns>
    public T SetIsGammaSpace(bool isGamma)
    {
        IsGammaSpace = isGamma;
        return (T)this;
    }
}
