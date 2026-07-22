using Aura3D.Core.Renderers;

namespace Aura3D.Core.Resources;

/// <summary>
/// Represents the writable texture type.
/// </summary>
public class WritableTexture : Texture
{
    /// <summary>
    /// Gets or sets the format.
    /// </summary>
    public TextureFormat Format { get; private set; } = TextureFormat.Rgba8;

    /// <summary>
    /// Initializes a new instance of the writable texture type.
    /// </summary>
    public WritableTexture()
    {
        ApplyFormatSettings(Format);
        WrapS = TextureWrapMode.ClampToEdge;
        WrapT = TextureWrapMode.ClampToEdge;
        MagFilter = TextureFilterMode.Linear;
        MinFilter = TextureFilterMode.Linear;
    }

    /// <summary>
    /// Sets the size.
    /// </summary>
    public WritableTexture SetSize(uint width, uint height)
    {
        if (Width == width && Height == height)
            return this;

        Width = width;
        Height = height;
        ClearPixelData();
        return this;
    }

    /// <summary>
    /// Sets the format.
    /// </summary>
    public WritableTexture SetFormat(TextureFormat format)
    {
        if (format is TextureFormat.DepthComponent16
            or TextureFormat.DepthComponent24
            or TextureFormat.DepthComponent32f
            or TextureFormat.Depth24Stencil8
            or TextureFormat.Depth32fStencil8)
        {
            throw Aura3D.Core.Exceptions.ResourceErrors.WritableTextureColorFormatOnly(nameof(format));
        }

        if (Format == format)
            return this;

        Format = format;
        ApplyFormatSettings(format);
        ClearPixelData();
        return this;
    }

    /// <summary>
    /// Clones the associated data.
    /// </summary>
    public new WritableTexture Clone()
    {
        var clone = new WritableTexture()
            .SetFormat(Format)
            .SetSize(Width, Height);
        clone.WrapS = WrapS;
        clone.WrapT = WrapT;
        clone.MinFilter = MinFilter;
        clone.MagFilter = MagFilter;
        return clone;
    }

    /// <summary>
    /// Deep-clones the associated data.
    /// </summary>
    public new WritableTexture DeepClone() => Clone();

    private void ApplyFormatSettings(TextureFormat format)
    {
        switch (format)
        {
            case TextureFormat.Rgb8:
                SetColorFormat(ColorFormat.RGB);
                SetIsGammaSpace(false);
                IsHdr = false;
                break;
            case TextureFormat.Srgb8:
                SetColorFormat(ColorFormat.RGB);
                SetIsGammaSpace(true);
                IsHdr = false;
                break;
            case TextureFormat.Rgba8:
                SetColorFormat(ColorFormat.RGBA);
                SetIsGammaSpace(false);
                IsHdr = false;
                break;
            case TextureFormat.Srgb8Alpha8:
                SetColorFormat(ColorFormat.RGBA);
                SetIsGammaSpace(true);
                IsHdr = false;
                break;
            case TextureFormat.Rgb16f:
            case TextureFormat.Rgb32f:
                SetColorFormat(ColorFormat.RGB);
                SetIsGammaSpace(false);
                IsHdr = true;
                break;
            case TextureFormat.Rgba16f:
            case TextureFormat.Rgba32f:
                SetColorFormat(ColorFormat.RGBA);
                SetIsGammaSpace(false);
                IsHdr = true;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
        }
    }
}
