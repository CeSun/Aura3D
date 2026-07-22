using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using System.Drawing;

namespace Aura3D.Core.Renderers;

/// <summary>
/// Represents the render output ref type.
/// </summary>
public abstract class RenderOutputRef
{
    internal abstract ResolvedRenderOutput Resolve(RenderPipeline renderPipeline, Camera camera);

    internal virtual RenderTarget ResolveRenderTarget(RenderPipeline renderPipeline, Camera camera)
    {
        throw Aura3D.Core.Exceptions.RendererErrors.InvalidRenderOutput();
    }
}

/// <summary>
/// Represents the camera output ref type.
/// </summary>
public sealed class CameraOutputRef : RenderOutputRef
{
    internal static CameraOutputRef Instance { get; } = new CameraOutputRef();

    private CameraOutputRef()
    {
    }

    internal override ResolvedRenderOutput Resolve(RenderPipeline renderPipeline, Camera camera)
    {
        return new ResolvedRenderOutput(
            renderPipeline.GetCameraFramebufferId(camera),
            camera.Width,
            camera.Height);
    }
}

/// <summary>
/// Represents the render target handle type.
/// </summary>
public sealed class RenderTargetHandle : RenderOutputRef
{
    private readonly HashSet<string> textureNames = [];

    internal RenderTargetHandle(RenderPipeline ownerPipeline, string name)
    {
        OwnerPipeline = ownerPipeline;
        Name = name;
    }

    internal RenderPipeline OwnerPipeline { get; }

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; }

    internal List<(string, TextureFormat)> Textures { get; } = [];

    /// <summary>
    /// Gets or sets the depth texture format.
    /// </summary>
    public TextureFormat DepthTextureFormat { get; private set; }

    /// <summary>
    /// Adds the texture.
    /// </summary>
    public RenderTargetHandle AddTexture(string name, TextureFormat internalFormat)
    {
        if (textureNames.Contains(name))
            throw Aura3D.Core.Exceptions.RendererErrors.TextureAlreadyRegistered(name, nameof(name));

        Textures.Add((name, internalFormat));
        textureNames.Add(name);
        return this;
    }

    /// <summary>
    /// Sets the depth texture.
    /// </summary>
    public RenderTargetHandle SetDepthTexture(TextureFormat textureFormat)
    {
        DepthTextureFormat = textureFormat;
        return this;
    }

    /// <summary>
    /// Gets the texture.
    /// </summary>
    public RenderTargetTextureHandle GetTexture(string name)
    {
        if (!textureNames.Contains(name))
        {
            throw Aura3D.Core.Exceptions.RendererErrors.TextureNotRegistered(name, Name);
        }

        return new RenderTargetTextureHandle(this, name);
    }

    internal override ResolvedRenderOutput Resolve(RenderPipeline renderPipeline, Camera camera)
    {
        var renderTarget = ResolveRenderTarget(renderPipeline, camera);
        return new ResolvedRenderOutput(renderTarget.FrameBufferId, renderTarget.Width, renderTarget.Height);
    }

    internal override RenderTarget ResolveRenderTarget(RenderPipeline renderPipeline, Camera camera)
    {
        if (!ReferenceEquals(OwnerPipeline, renderPipeline))
        {
            throw Aura3D.Core.Exceptions.RendererErrors.RenderTargetOwnershipMismatch();
        }

        return renderPipeline.GetRenderTarget(this, new Size((int)camera.Width, (int)camera.Height));
    }
}

/// <summary>
/// Represents the render target texture handle type.
/// </summary>
public sealed class RenderTargetTextureHandle
{
    internal RenderTargetTextureHandle(RenderTargetHandle renderTarget, string textureName)
    {
        RenderTarget = renderTarget;
        TextureName = textureName;
    }

    /// <summary>
    /// Gets the render target.
    /// </summary>
    public RenderTargetHandle RenderTarget { get; }

    /// <summary>
    /// Gets the texture name.
    /// </summary>
    public string TextureName { get; }

    internal RenderTexture ResolveTexture(RenderPipeline renderPipeline, Camera camera)
    {
        var renderTarget = RenderTarget.ResolveRenderTarget(renderPipeline, camera);
        return renderTarget.GetTexture(TextureName)!;
    }
}

internal readonly struct ResolvedRenderOutput
{
    public ResolvedRenderOutput(uint framebufferId, uint width, uint height)
    {
        FramebufferId = framebufferId;
        Width = width;
        Height = height;
    }

    public uint FramebufferId { get; }

    public uint Width { get; }

    public uint Height { get; }
}
