using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;

namespace Aura3D.Core.Renderers;

/// <summary>
/// 渲染目标类，用于管理帧缓冲对象及其关联的颜色纹理和深度/模板纹理。
/// </summary>
public class RenderTarget : IGpuState
{
    /// <summary>
    /// 初始化 <see cref="RenderTarget"/> 类的新实例。
    /// </summary>
    public RenderTarget()
    {
        depthStencilTexture = new RenderTexture(this);
    }

    /// <inheritedoc />
    public int MipmapLevel { get; private set; } = 1;

    /// <summary>
    /// 设置渲染目标的 Mipmap 级别。
    /// </summary>
    /// <param name="mipmapLevel">Mipmap 级别。</param>
    /// <returns>当前的 <see cref="RenderTarget"/> 实例。</returns>
    public RenderTarget SetMipMapLevel(int mipmapLevel)
    {
        MipmapLevel = mipmapLevel;

        return this;
    }

    protected List<RenderTexture> renderTextures = new List<RenderTexture>();

    protected Dictionary<string, RenderTexture> renderTexturesMap = new Dictionary<string, RenderTexture>();

    protected RenderTexture depthStencilTexture { get;  set; }

    /// <inheritedoc />
    public RenderTexture DepthStencilTexture => depthStencilTexture;

    /// <summary>
    /// 获取或设置帧缓冲对象的 ID。
    /// </summary>
    public uint FrameBufferId { get; set; }

    /// <summary>
    /// 获取或设置渲染目标的高度。
    /// </summary>
    public uint Height { get; set; }

    /// <summary>
    /// 获取或设置渲染目标的宽度。
    /// </summary>
    public uint Width { get; set; }

    /// <inheritedoc />
    public float Scale { get; set; } = 1.0f;

    /// <summary>
    /// 设置渲染目标的尺寸。
    /// </summary>
    /// <param name="width">宽度。</param>
    /// <param name="height">高度。</param>
    /// <returns>当前的 <see cref="RenderTarget"/> 实例。</returns>
    public RenderTarget SetSize(uint width, uint height)
    {
        Width = width;
        Height = height;
        SyncTextureSizes();
        return this;
    }


    /// <summary>
    /// 销毁渲染目标及其关联的所有 GPU 资源。
    /// </summary>
    /// <param name="gl">OpenGL 上下文。</param>
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
        if (DepthStencilTexture.TextureId != 0)
            gl.DeleteTexture(DepthStencilTexture.TextureId);

        if (FrameBufferId != 0)
        {
            gl.DeleteFramebuffer(FrameBufferId);
        }


    }

    /// <summary>
    /// 上传渲染目标数据到 GPU。
    /// </summary>
    /// <param name="gl">OpenGL 上下文。</param>
    /// <exception cref="InvalidOperationException">当帧缓冲创建失败时抛出。</exception>
    public unsafe void Upload(GL gl)
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

            if (MipmapLevel > 1)
                gl.GenerateMipmap(GLEnum.TextureCubeMap);
            gl.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.ColorAttachment0 + index, GLEnum.Texture2D, texture.TextureId, 0);

            ColorAttachmentSet[index] = GLEnum.ColorAttachment0 + index;
            index++;
        }

        depthStencilTexture.TextureId = gl.GenTexture();
        gl.BindTexture(GLEnum.Texture2D, DepthStencilTexture.TextureId);

        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Nearest);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Nearest);

        gl.TexImage2D(GLEnum.Texture2D, 0, (int)depthStencilTexture.InternalFormat.ToGlInternalFormat(), (uint)Width, (uint)Height, 0, depthStencilTexture.InternalFormat.ToGlPixelFormat(), depthStencilTexture.InternalFormat.ToGlPixelType(), (void*)0);

        if (MipmapLevel > 1)
            gl.GenerateMipmap(GLEnum.TextureCubeMap);
        gl.FramebufferTexture2D(GLEnum.Framebuffer, depthStencilTexture.InternalFormat.ToGlAttachment(), GLEnum.Texture2D, DepthStencilTexture.TextureId, 0);

        gl.DrawBuffers(ColorAttachmentSet);
        state = gl.CheckFramebufferStatus(GLEnum.Framebuffer);


        if (state != GLEnum.FramebufferComplete)
        {
            throw new InvalidOperationException($"Framebuffer creation failed with status: {state}");
        }

        SyncTextureSizes();
    }

    /// <summary>
    /// 向渲染目标添加一个颜色纹理。
    /// </summary>
    /// <param name="name">纹理名称。</param>
    /// <param name="internalFormat">纹理内部格式。</param>
    /// <returns>当前的 <see cref="RenderTarget"/> 实例。</returns>
    public RenderTarget AddRenderTexture(string name, TextureFormat internalFormat)
    {
        renderTextures.Add(new RenderTexture(this)
        {
            InternalFormat = internalFormat
        });
        renderTexturesMap.Add(name, renderTextures.Last());

        return this;
    }


    /// <summary>
    /// 获取指定索引的颜色纹理。
    /// </summary>
    /// <param name="index">纹理索引。</param>
    /// <returns>纹理实例，如果索引无效则返回 null。</returns>
    public RenderTexture? GetTexture(int index)
    {
        if (index < 0)
            return null;
        if (index >= renderTextures.Count)
            return null;
        return renderTextures[index];
    }

    /// <summary>
    /// 获取指定名称的颜色纹理。
    /// </summary>
    /// <param name="name">纹理名称。</param>
    /// <returns>纹理实例。</returns>
    /// <exception cref="KeyNotFoundException">当纹理不存在时抛出。</exception>
    public RenderTexture GetTexture(string name)
    {
        if (renderTexturesMap.TryGetValue(name, out var texture))
        {
            return texture;
        }
        throw new KeyNotFoundException($"RenderTarget texture '{name}' not found");
    }

    /// <summary>
    /// 获取深度纹理的格式。
    /// </summary>
    public TextureFormat DepthTextureFormat { get; private set; }

    /// <summary>
    /// 设置深度纹理格式。
    /// </summary>
    /// <param name="textureFormat">深度纹理格式。</param>
    /// <returns>当前的 <see cref="RenderTarget"/> 实例。</returns>
    public RenderTarget SetDepthTexture(TextureFormat textureFormat)
    {
        depthStencilTexture.InternalFormat = textureFormat;
        DepthTextureFormat = textureFormat;
        return this;
    }

    private void SyncTextureSizes()
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
    /// 渲染目标内部的纹理实现类。
    /// </summary>
    public sealed class RenderTexture : Aura3D.Core.Resources.Texture
    {

        /// <summary>
        /// 初始化 <see cref="RenderTexture"/> 类的新实例。
        /// </summary>
        /// <param name="rt">所属的渲染目标。</param>
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
        /// 获取或设置纹理的内部格式。
        /// </summary>
        public TextureFormat InternalFormat { get; set; }
    }

}

public enum TextureFormat
{

    DepthComponent16,
    DepthComponent24,
    DepthComponent32f,
    Depth24Stencil8,
    Depth32fStencil8,

    Rgb8 ,
    Srgb8,
    Rgba8,
    Srgb8Alpha8,

    Rgb16f,
    Rgba16f,

    Rgb32f,
    Rgba32f,
}


public static class TextureFormatExtensions
{
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

        _ => throw new ArgumentOutOfRangeException(nameof(format), $"Texture format '{format}' is not supported.")
    };


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

        _ => throw new ArgumentOutOfRangeException(nameof(format), $"Texture format '{format}' is not supported.")

    };


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
        _ => throw new ArgumentOutOfRangeException(nameof(format), $"Texture format '{format}' is not supported.")
    };


    public static GLEnum ToGlAttachment(this TextureFormat format) => format switch
    {
        TextureFormat.DepthComponent16 => GLEnum.DepthAttachment,
        TextureFormat.DepthComponent24 => GLEnum.DepthAttachment,
        TextureFormat.DepthComponent32f => GLEnum.DepthAttachment,
        TextureFormat.Depth24Stencil8 => GLEnum.DepthStencilAttachment,
        TextureFormat.Depth32fStencil8 => GLEnum.DepthStencilAttachment,
        _ => throw new ArgumentOutOfRangeException(nameof(format), $"Texture format '{format}' is not supported.")
    };
}
