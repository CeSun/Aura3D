using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;

namespace Aura3D.Core.Renderers;

/// <summary>
/// Represents the render target type.
/// </summary>
public class RenderTarget : RenderTargetBase<RenderTexture, RenderTarget>
{
    /// <summary>
    /// Initializes a new instance of the render target type.
    /// </summary>
    public RenderTarget()
    {
        depthStencilTexture = new RenderTexture(this);
    }

    /// <summary>
    /// Adds the render texture.
    /// </summary>
    public override RenderTarget AddRenderTexture(string name, TextureFormat internalFormat)
    {
        renderTextures.Add(new RenderTexture(this)
        {
            InternalFormat = internalFormat
        });
        renderTexturesMap.Add(name, renderTextures[^1]);
        Version++;

        return this;
    }

    /// <summary>
    /// Uploads the associated data.
    /// </summary>
    public override unsafe void Upload(GL gl)
    {
        FrameBufferId = gl.GenFramebuffer();
        gl.BindFramebuffer(GLEnum.Framebuffer, FrameBufferId);

        int index = 0;

        GLEnum state = default;

        Span<GLEnum> ColorAttachmentSet = stackalloc GLEnum[renderTextures.Count];
        foreach (var texture in renderTextures)
        {
            texture.TextureId = gl.GenTexture();

            gl.BindTexture(GLEnum.Texture2D, texture.TextureId);

            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Linear);
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Linear);
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);

            gl.TexImage2D(GLEnum.Texture2D, 0, (int)texture.InternalFormat.ToGlInternalFormat(), (uint)Width, (uint)Height, 0, (GLEnum)texture.InternalFormat.ToGlPixelFormat(), (GLEnum)texture.InternalFormat.ToGlPixelType(), null);

            if (EnableMipMap)
                gl.GenerateMipmap(GLEnum.Texture2D);
            gl.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.ColorAttachment0 + index, GLEnum.Texture2D, texture.TextureId, 0);

            ColorAttachmentSet[index] = GLEnum.ColorAttachment0 + index;
            index++;
        }

        depthStencilTexture.TextureId = gl.GenTexture();
        gl.BindTexture(GLEnum.Texture2D, DepthStencilTexture.TextureId);

        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Nearest);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Nearest);

        gl.TexImage2D(GLEnum.Texture2D, 0, (int)depthStencilTexture.InternalFormat.ToGlInternalFormat(), (uint)Width, (uint)Height, 0, depthStencilTexture.InternalFormat.ToGlPixelFormat(), depthStencilTexture.InternalFormat.ToGlPixelType(), (void*)0);

        gl.FramebufferTexture2D(GLEnum.Framebuffer, depthStencilTexture.InternalFormat.ToGlAttachment(), GLEnum.Texture2D, DepthStencilTexture.TextureId, 0);

        gl.DrawBuffers(ColorAttachmentSet);
        state = gl.CheckFramebufferStatus(GLEnum.Framebuffer);


        if (state != GLEnum.FramebufferComplete)
        {
            throw Aura3D.Core.Exceptions.RendererErrors.FramebufferCreationFailed(state, nameof(RenderTarget));
        }

        SyncTextureSizes();
        SyncedVersion = Version;
    }
}


/// <summary>
/// Represents the render texture type.
/// </summary>
public sealed class RenderTexture : Aura3D.Core.Resources.Texture, IRenderTargetTexture
{

    /// <summary>
    /// Initializes a new instance of the render texture type.
    /// </summary>
    public RenderTexture(RenderTarget rt)
    {
        RenderTarget = rt;
        WrapS = Aura3D.Core.Resources.TextureWrapMode.ClampToEdge;
        WrapT = Aura3D.Core.Resources.TextureWrapMode.ClampToEdge;
        MinFilter = Aura3D.Core.Resources.TextureFilterMode.Linear;
        MagFilter = Aura3D.Core.Resources.TextureFilterMode.Linear;
    }

    RenderTarget RenderTarget { get; set; }

    internal TextureGpuState? CachedGpuState { get; set; }

    /// <inheritedoc />
    public uint TextureId { get; set; }

    /// <summary>
    /// Gets or sets the internal format.
    /// </summary>
    public TextureFormat InternalFormat { get; set; }
}

/// <summary>
/// Specifies values for texture format.
/// </summary>
public enum TextureFormat
{

    /// <summary>
    /// Specifies depth component16.
    /// </summary>
    DepthComponent16,
    /// <summary>
    /// Specifies depth component24.
    /// </summary>
    DepthComponent24,
    /// <summary>
    /// Specifies depth component32f.
    /// </summary>
    DepthComponent32f,
    /// <summary>
    /// Specifies depth24 stencil8.
    /// </summary>
    Depth24Stencil8,
    /// <summary>
    /// Specifies depth32f stencil8.
    /// </summary>
    Depth32fStencil8,

    /// <summary>
    /// Specifies rgb8.
    /// </summary>
    Rgb8 ,
    /// <summary>
    /// Specifies srgb8.
    /// </summary>
    Srgb8,
    /// <summary>
    /// Specifies rgba8.
    /// </summary>
    Rgba8,
    /// <summary>
    /// Specifies srgb8 alpha8.
    /// </summary>
    Srgb8Alpha8,

    /// <summary>
    /// Specifies rgb16f.
    /// </summary>
    Rgb16f,
    /// <summary>
    /// Specifies rgba16f.
    /// </summary>
    Rgba16f,

    /// <summary>
    /// Specifies rgb32f.
    /// </summary>
    Rgb32f,
    /// <summary>
    /// Specifies rgba32f.
    /// </summary>
    Rgba32f,
}


/// <summary>
/// Represents the texture format extensions type.
/// </summary>
public static class TextureFormatExtensions
{
    /// <summary>
    /// Performs the to gl pixel type operation.
    /// </summary>
    public static PixelType ToGlPixelType(this TextureFormat format) => format switch
    {
        TextureFormat.DepthComponent16 => PixelType.UnsignedShort,
        TextureFormat.DepthComponent24 => PixelType.UnsignedInt,
        TextureFormat.DepthComponent32f => PixelType.Float,
        TextureFormat.Depth24Stencil8 => PixelType.UnsignedInt248,
        TextureFormat.Depth32fStencil8 => PixelType.Float32UnsignedInt248Rev,

        TextureFormat.Rgb8 => PixelType.UnsignedByte,
        TextureFormat.Srgb8 => PixelType.UnsignedByte,
        TextureFormat.Rgba8 => PixelType.UnsignedByte,
        TextureFormat.Srgb8Alpha8 => PixelType.UnsignedByte,

        TextureFormat.Rgb16f => PixelType.HalfFloat,
        TextureFormat.Rgba16f => PixelType.HalfFloat,

        TextureFormat.Rgb32f => PixelType.Float,
        TextureFormat.Rgba32f => PixelType.Float,

        _ => throw Aura3D.Core.Exceptions.RendererErrors.UnsupportedTextureFormat(nameof(format), format)
    };


    /// <summary>
    /// Performs the to gl pixel format operation.
    /// </summary>
    public static PixelFormat ToGlPixelFormat(this TextureFormat format) => format switch
    {
        TextureFormat.DepthComponent16 => PixelFormat.DepthComponent,
        TextureFormat.DepthComponent24 => PixelFormat.DepthComponent,
        TextureFormat.DepthComponent32f => PixelFormat.DepthComponent,
        TextureFormat.Depth24Stencil8 => PixelFormat.DepthStencil,
        TextureFormat.Depth32fStencil8 => PixelFormat.DepthStencil,

        TextureFormat.Rgb8 => PixelFormat.Rgb,
        TextureFormat.Srgb8 => PixelFormat.Rgb,
        TextureFormat.Rgba8 => PixelFormat.Rgba,
        TextureFormat.Srgb8Alpha8 => PixelFormat.Rgba,

        TextureFormat.Rgb16f => PixelFormat.Rgb,
        TextureFormat.Rgba16f => PixelFormat.Rgba,

        TextureFormat.Rgb32f => PixelFormat.Rgb,
        TextureFormat.Rgba32f => PixelFormat.Rgba,

        _ => throw Aura3D.Core.Exceptions.RendererErrors.UnsupportedTextureFormat(nameof(format), format),

    };


    /// <summary>
    /// Performs the to gl internal format operation.
    /// </summary>
    public static InternalFormat ToGlInternalFormat(this TextureFormat format) => format switch
    {
        TextureFormat.DepthComponent16 => InternalFormat.DepthComponent16,
        TextureFormat.DepthComponent24 => InternalFormat.DepthComponent24,
        TextureFormat.DepthComponent32f => InternalFormat.DepthComponent32f,
        TextureFormat.Depth24Stencil8 => InternalFormat.Depth24Stencil8,
        TextureFormat.Depth32fStencil8 => InternalFormat.Depth32fStencil8,
        TextureFormat.Rgb8 => InternalFormat.Rgb8,
        TextureFormat.Srgb8 => InternalFormat.Srgb8,
        TextureFormat.Rgba8 => InternalFormat.Rgba8,
        TextureFormat.Srgb8Alpha8 => InternalFormat.Srgb8Alpha8,
        TextureFormat.Rgb16f => InternalFormat.Rgb16f,
        TextureFormat.Rgba16f => InternalFormat.Rgba16f,
        TextureFormat.Rgb32f => InternalFormat.Rgb32f,
        TextureFormat.Rgba32f => InternalFormat.Rgba32f,
        _ => throw Aura3D.Core.Exceptions.RendererErrors.UnsupportedTextureFormat(nameof(format), format)
    };


    /// <summary>
    /// Performs the to gl attachment operation.
    /// </summary>
    public static GLEnum ToGlAttachment(this TextureFormat format) => format switch
    {
        TextureFormat.DepthComponent16 => GLEnum.DepthAttachment,
        TextureFormat.DepthComponent24 => GLEnum.DepthAttachment,
        TextureFormat.DepthComponent32f => GLEnum.DepthAttachment,
        TextureFormat.Depth24Stencil8 => GLEnum.DepthStencilAttachment,
        TextureFormat.Depth32fStencil8 => GLEnum.DepthStencilAttachment,
        _ => throw Aura3D.Core.Exceptions.RendererErrors.UnsupportedTextureFormat(nameof(format), format)
    };
}
