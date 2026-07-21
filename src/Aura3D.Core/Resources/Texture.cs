using Silk.NET.OpenGLES;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Aura3D.Core.Resources;

/// <summary>
/// 纹理类，支持2D纹理的加载、上传和渲染
/// </summary>
public class Texture : BaseTexture<Texture>, IClone<Texture>
{
    private PixelState _pixelState = new();

    public override ulong Version
    {
        get => unchecked(base.Version + _pixelState.Version);
        protected set => base.Version = unchecked(value - _pixelState.Version);
    }

    public override uint Width
    {
        get => _pixelState.Width;
        set => UpdatePixelMetadata(value, _pixelState.Height, _pixelState.IsHdr);
    }

    public override uint Height
    {
        get => _pixelState.Height;
        set => UpdatePixelMetadata(_pixelState.Width, value, _pixelState.IsHdr);
    }

    public override bool IsHdr
    {
        get => _pixelState.IsHdr;
        set => UpdatePixelMetadata(_pixelState.Width, _pixelState.Height, value);
    }

    /// <summary>
    /// 从颜色创建纯色纹理
    /// </summary>
    /// <param name="color">颜色</param>
    /// <returns>纯色纹理</returns>
    public static Texture CreateFromColor(Color color)
    {
        var texture = new Resources.Texture();
        texture.SetLdrData(
        [
            color.R, color.G, color.B, color.A,
            color.R, color.G, color.B, color.A,
            color.R, color.G, color.B, color.A,
            color.R, color.G, color.B, color.A,
        ], 2, 2);
        texture.SetIsGammaSpace(false);
        texture.SetColorFormat(ColorFormat.RGBA);
        texture.MagFilter = TextureFilterMode.Nearest;
        texture.MinFilter = TextureFilterMode.Nearest;
        texture.WrapS = TextureWrapMode.Repeat;
        texture.WrapT = TextureWrapMode.Repeat;

        return texture;


    }

    public ReadOnlySpan<byte> AsLdrData() => IsHdr ? [] : CollectionsMarshal.AsSpan(_pixelState.Data);

    public ReadOnlySpan<float> AsHdrData() => IsHdr ? MemoryMarshal.Cast<byte, float>(CollectionsMarshal.AsSpan(_pixelState.Data)) : [];

    public Texture SetLdrData(ReadOnlySpan<byte> data, uint width, uint height)
    {
        _pixelState.Replace(data, width, height, isHdr: false);
        return this;
    }

    public Texture SetHdrData(ReadOnlySpan<float> data, uint width, uint height)
    {
        _pixelState.Replace(MemoryMarshal.AsBytes(data), width, height, isHdr: true);
        return this;
    }

    public Texture Clone()
    {
        var texture = new Texture
        {
            Width = Width,
            Height = Height,
            IsHdr = IsHdr,
            WrapS = WrapS,
            WrapT = WrapT,
            MinFilter = MinFilter,
            MagFilter = MagFilter,
            ColorFormat = ColorFormat,
            IsGammaSpace = IsGammaSpace,
        };

        texture.SetPixelState(_pixelState);
        return texture;
    }

    public Texture DeepClone()
    {
        var texture = Clone();
        texture.SetPixelState(new PixelState(_pixelState));
        return texture;
    }

    protected void SetPixelBuffer(List<byte> data)
    {
        _pixelState = new PixelState(
            data,
            _pixelState.Width,
            _pixelState.Height,
            _pixelState.IsHdr,
            unchecked(_pixelState.Version + 1));
    }

    private void SetPixelState(PixelState pixelState)
    {
        _pixelState = pixelState;
    }

    protected void ClearPixelData()
    {
        _pixelState.Replace([], _pixelState.Width, _pixelState.Height, _pixelState.IsHdr);
    }

    private void UpdatePixelMetadata(uint width, uint height, bool isHdr)
    {
        if (_pixelState.Width == width && _pixelState.Height == height && _pixelState.IsHdr == isHdr)
            return;

        _pixelState.UpdateMetadata(width, height, isHdr);
    }

    private sealed class PixelState
    {
        public PixelState()
        {
        }

        public PixelState(PixelState source)
            : this(new List<byte>(source.Data), source.Width, source.Height, source.IsHdr, source.Version)
        {
        }

        public PixelState(List<byte> data, uint width, uint height, bool isHdr, ulong version = 1)
        {
            Data = data;
            Width = width;
            Height = height;
            IsHdr = isHdr;
            Version = version;
        }

        public List<byte> Data { get; } = [];

        public uint Width { get; private set; }

        public uint Height { get; private set; }

        public bool IsHdr { get; private set; }

        public ulong Version { get; private set; } = 1;

        public void Replace(ReadOnlySpan<byte> data, uint width, uint height, bool isHdr)
        {
            var snapshot = data.ToArray();
            Data.EnsureCapacity(snapshot.Length);
            Data.Clear();
            Data.AddRange(snapshot);
            Width = width;
            Height = height;
            IsHdr = isHdr;
            Version++;
        }

        public void UpdateMetadata(uint width, uint height, bool isHdr)
        {
            Width = width;
            Height = height;
            IsHdr = isHdr;
            Version++;
        }
    }

}

/// <summary>
/// 颜色格式枚举
/// </summary>
public enum ColorFormat
{
    RGB = 0,
    RGBA = 1,
}

/// <summary>
/// 纹理环绕模式枚举
/// </summary>
public enum TextureWrapMode
{
    /// <summary>
    /// 重复
    /// </summary>
    Repeat = 0,
    /// <summary>
    /// 镜像重复
    /// </summary>
    MirroredRepeat = 1,
    /// <summary>
    /// 钳制到边缘
    /// </summary>
    ClampToEdge = 2,
    /// <summary>
    /// 钳制到边界颜色
    /// </summary>
    ClampToBorder = 3,
}

/// <summary>
/// 纹理过滤模式枚举
/// </summary>
public enum TextureFilterMode
{
    /// <summary>
    /// 最近邻过滤
    /// </summary>
    Nearest = 0,
    /// <summary>
    /// 线性过滤
    /// </summary>
    Linear = 1,
}
