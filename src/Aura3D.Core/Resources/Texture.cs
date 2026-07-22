using Silk.NET.OpenGLES;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Aura3D.Core.Resources;

/// <summary>
/// Represents the texture type.
/// </summary>
public class Texture : BaseTexture<Texture>, IClone<Texture>
{
    private PixelState _pixelState = new();

    /// <summary>
    /// Gets the version.
    /// </summary>
    public override ulong Version
    {
        get => unchecked(base.Version + _pixelState.Version);
        protected set => base.Version = unchecked(value - _pixelState.Version);
    }

    /// <summary>
    /// Gets the width.
    /// </summary>
    public override uint Width
    {
        get => _pixelState.Width;
        set => UpdatePixelMetadata(value, _pixelState.Height, _pixelState.IsHdr);
    }

    /// <summary>
    /// Gets the height.
    /// </summary>
    public override uint Height
    {
        get => _pixelState.Height;
        set => UpdatePixelMetadata(_pixelState.Width, value, _pixelState.IsHdr);
    }

    /// <summary>
    /// Gets a value indicating whether the object is hdr.
    /// </summary>
    public override bool IsHdr
    {
        get => _pixelState.IsHdr;
        set => UpdatePixelMetadata(_pixelState.Width, _pixelState.Height, value);
    }

    /// <summary>
    /// Creates the from color.
    /// </summary>
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

    /// <summary>
    /// Performs the as ldr data operation.
    /// </summary>
    public ReadOnlySpan<byte> AsLdrData() => IsHdr ? [] : CollectionsMarshal.AsSpan(_pixelState.Data);

    /// <summary>
    /// Performs the as hdr data operation.
    /// </summary>
    public ReadOnlySpan<float> AsHdrData() => IsHdr ? MemoryMarshal.Cast<byte, float>(CollectionsMarshal.AsSpan(_pixelState.Data)) : [];

    /// <summary>
    /// Sets the ldr data.
    /// </summary>
    public Texture SetLdrData(ReadOnlySpan<byte> data, uint width, uint height)
    {
        _pixelState.Replace(data, width, height, isHdr: false);
        return this;
    }

    /// <summary>
    /// Sets the hdr data.
    /// </summary>
    public Texture SetHdrData(ReadOnlySpan<float> data, uint width, uint height)
    {
        _pixelState.Replace(MemoryMarshal.AsBytes(data), width, height, isHdr: true);
        return this;
    }

    /// <summary>
    /// Clones the associated data.
    /// </summary>
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

    /// <summary>
    /// Deep-clones the associated data.
    /// </summary>
    public Texture DeepClone()
    {
        var texture = Clone();
        texture.SetPixelState(new PixelState(_pixelState));
        return texture;
    }

    /// <summary>
    /// Sets the pixel buffer.
    /// </summary>
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

    /// <summary>
    /// Clears the pixel data.
    /// </summary>
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
/// Specifies values for color format.
/// </summary>
public enum ColorFormat
{
    /// <summary>
    /// Gets the rgb.
    /// </summary>
    RGB = 0,
    /// <summary>
    /// Gets the rgba.
    /// </summary>
    RGBA = 1,
}

/// <summary>
/// Specifies values for texture wrap mode.
/// </summary>
public enum TextureWrapMode
{
    /// <summary>
    /// Gets the repeat.
    /// </summary>
    Repeat = 0,
    /// <summary>
    /// Gets the mirrored repeat.
    /// </summary>
    MirroredRepeat = 1,
    /// <summary>
    /// Gets the clamp to edge.
    /// </summary>
    ClampToEdge = 2,
    /// <summary>
    /// Gets the clamp to border.
    /// </summary>
    ClampToBorder = 3,
}

/// <summary>
/// Specifies values for texture filter mode.
/// </summary>
public enum TextureFilterMode
{
    /// <summary>
    /// Gets the nearest.
    /// </summary>
    Nearest = 0,
    /// <summary>
    /// Gets the linear.
    /// </summary>
    Linear = 1,
}
