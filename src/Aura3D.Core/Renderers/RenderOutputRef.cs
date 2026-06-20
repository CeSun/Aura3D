using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using System.Drawing;

namespace Aura3D.Core.Renderers;

/// <summary>
/// 渲染输出引用，用于统一表示相机最终输出或已注册的渲染目标。
/// </summary>
public abstract class RenderOutputRef
{
    internal abstract ResolvedRenderOutput Resolve(RenderPipeline renderPipeline, Camera camera);

    internal virtual RenderTarget ResolveRenderTarget(RenderPipeline renderPipeline, Camera camera)
    {
        throw new InvalidOperationException("Current render output is not a registered render target.");
    }
}

/// <summary>
/// 相机最终输出引用，会根据相机配置自动输出到相机纹理或默认 surface。
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
/// 已注册渲染目标的引用与配置对象。
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

    public string Name { get; }

    internal List<(string, TextureFormat)> Textures { get; } = [];

    public TextureFormat DepthTextureFormat { get; private set; }

    public RenderTargetHandle AddTexture(string name, TextureFormat internalFormat)
    {
        if (textureNames.Contains(name))
            throw new ArgumentException($"Texture '{name}' already exists in render target configuration.", nameof(name));

        Textures.Add((name, internalFormat));
        textureNames.Add(name);
        return this;
    }

    public RenderTargetHandle SetDepthTexture(TextureFormat textureFormat)
    {
        DepthTextureFormat = textureFormat;
        return this;
    }

    public RenderTargetTextureHandle GetTexture(string name)
    {
        if (!textureNames.Contains(name))
        {
            throw new KeyNotFoundException($"Texture '{name}' is not registered in render target '{Name}'.");
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
            throw new InvalidOperationException("Render target handle does not belong to the current render pipeline.");
        }

        return renderPipeline.GetRenderTarget(this, new Size((int)camera.Width, (int)camera.Height));
    }
}

/// <summary>
/// 已注册渲染目标上的纹理附件引用。
/// </summary>
public sealed class RenderTargetTextureHandle
{
    internal RenderTargetTextureHandle(RenderTargetHandle renderTarget, string textureName)
    {
        RenderTarget = renderTarget;
        TextureName = textureName;
    }

    public RenderTargetHandle RenderTarget { get; }

    public string TextureName { get; }

    internal IGpuTexture ResolveTexture(RenderPipeline renderPipeline, Camera camera)
    {
        var renderTarget = RenderTarget.ResolveRenderTarget(renderPipeline, camera);
        return renderTarget.GetTexture(TextureName);
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
