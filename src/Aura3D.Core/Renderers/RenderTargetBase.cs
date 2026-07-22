using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;

namespace Aura3D.Core.Renderers;

/// <summary>
/// Defines the contract for render target texture.
/// </summary>
public interface IRenderTargetTexture
{
    /// <summary>
    /// Gets or sets the texture id.
    /// </summary>
    uint TextureId { get; set; }

    /// <summary>
    /// Gets or sets the internal format.
    /// </summary>
    TextureFormat InternalFormat { get; set; }

    /// <summary>
    /// Gets or sets the width.
    /// </summary>
    uint Width { get; set; }

    /// <summary>
    /// Gets or sets the height.
    /// </summary>
    uint Height { get; set; }
}

/// <summary>
/// Represents the render target base type.
/// </summary>
public abstract class RenderTargetBase<TTexture, TSelf> : IRuntimeGpuState
    where TTexture : class, IRenderTargetTexture
    where TSelf : RenderTargetBase<TTexture, TSelf>
{
    /// <inheritdoc />
    public ulong Version { get; protected set; } = 1;

    /// <inheritdoc />
    public ulong SyncedVersion { get; protected set; }

    /// <summary>
    /// Performs the new operation.
    /// </summary>
    protected List<TTexture> renderTextures = new();

    /// <summary>
    /// Performs the new operation.
    /// </summary>
    protected Dictionary<string, TTexture> renderTexturesMap = new();

    /// <summary>
    /// Gets the depth stencil texture.
    /// </summary>
    protected TTexture depthStencilTexture = default!;

    /// <summary>
    /// Gets the depth stencil texture.
    /// </summary>
    public TTexture DepthStencilTexture => depthStencilTexture;

    /// <summary>
    /// Gets or sets the frame buffer id.
    /// </summary>
    public uint FrameBufferId { get; set; }

    /// <summary>
    /// Gets or sets the height.
    /// </summary>
    public uint Height { get; set; }

    /// <summary>
    /// Gets or sets the width.
    /// </summary>
    public uint Width { get; set; }

    /// <inheritdoc />
    public float Scale { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets the enable mip map.
    /// </summary>
    public bool EnableMipMap { get; set; }

    /// <summary>
    /// Gets or sets the depth texture format.
    /// </summary>
    public TextureFormat DepthTextureFormat { get; protected set; }

    /// <summary>
    /// Sets the enable mip map level.
    /// </summary>
    public TSelf SetEnableMipMapLevel(bool enableMipMap)
    {
        if (EnableMipMap == enableMipMap)
            return (TSelf)this;
        EnableMipMap = enableMipMap;
        Version++;
        return (TSelf)this;
    }

    /// <summary>
    /// Sets the size.
    /// </summary>
    public TSelf SetSize(uint width, uint height)
    {
        if (Width == width && Height == height)
            return (TSelf)this;

        Width = width;
        Height = height;
        SyncTextureSizes();
        Version++;
        return (TSelf)this;
    }

    /// <summary>
    /// Gets the texture.
    /// </summary>
    public TTexture? GetTexture(int index)
    {
        if (index < 0)
            return null;
        if (index >= renderTextures.Count)
            return null;
        return renderTextures[index];
    }

    /// <summary>
    /// Gets the texture.
    /// </summary>
    public TTexture? GetTexture(string name)
    {
        if (renderTexturesMap.TryGetValue(name, out var texture))
            return texture;
        return null;
    }

    /// <summary>
    /// Sets the depth texture.
    /// </summary>
    public TSelf SetDepthTexture(TextureFormat textureFormat)
    {
        if (DepthTextureFormat == textureFormat)
            return (TSelf)this;

        depthStencilTexture.InternalFormat = textureFormat;
        DepthTextureFormat = textureFormat;
        Version++;
        return (TSelf)this;
    }

    /// <summary>
    /// Adds the render texture.
    /// </summary>
    public abstract TSelf AddRenderTexture(string name, TextureFormat internalFormat);

    /// <summary>
    /// Performs the sync texture sizes operation.
    /// </summary>
    protected void SyncTextureSizes()
    {
        foreach (var texture in renderTextures)
        {
            texture.Width = Width;
            texture.Height = Height;
        }

        depthStencilTexture.Width = Width;
        depthStencilTexture.Height = Height;
    }

    /// <summary>
    /// Destroys the associated data.
    /// </summary>
    public void Destroy(GL gl)
    {
        foreach (var texture in renderTextures)
        {
            if (texture.TextureId != 0)
            {
                gl.DeleteTexture(texture.TextureId);
                texture.TextureId = 0;
            }
        }
        if (depthStencilTexture.TextureId != 0)
            gl.DeleteTexture(depthStencilTexture.TextureId);

        if (FrameBufferId != 0)
        {
            gl.DeleteFramebuffer(FrameBufferId);
        }

        SyncedVersion = 0;
    }

    /// <summary>
    /// Uploads the associated data.
    /// </summary>
    public abstract void Upload(GL gl);
}
