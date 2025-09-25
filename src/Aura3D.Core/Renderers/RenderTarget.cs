using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;

namespace Aura3D.Core.Renderers;

public class RenderTarget : IGpuResource, IRenderTarget
{
    public RenderTarget()
    {
        depthStencilTexture = new RenderTexture(this);
    }

    protected List<RenderTexture> renderTextures = new List<RenderTexture>();

    protected Dictionary<string, RenderTexture> renderTexturesMap = new Dictionary<string, RenderTexture>();

    protected RenderTexture depthStencilTexture { get;  set; }

    public ITexture DepthStencilTexture => depthStencilTexture;

    public bool NeedsUpload { get; set; } = true;

    public uint FrameBufferId { get; set; }

    public uint Height { get; set; }

    public uint Width { get; set; }

    public RenderTarget SetSize(uint width, uint height)
    {
        Width = width;
        Height = height;
        NeedsUpload = true;
        return this;
    }


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
            gl.TexImage2D(GLEnum.Texture2D, 0, (int)texture.InternalFormat.ToGlInternalFormat(), Width, Height, 0, (GLEnum)texture.InternalFormat.ToGlPixelFormat(), (GLEnum)texture.InternalFormat.ToGlPixelType(), null);
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Linear);
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Linear);
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);
            gl.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.ColorAttachment0 + index, GLEnum.Texture2D, texture.TextureId, 0);
            ColorAttachmentSet[index] = GLEnum.ColorAttachment0 + index;
            index++;
        }

        depthStencilTexture.TextureId = gl.GenTexture();
        gl.BindTexture(GLEnum.Texture2D, DepthStencilTexture.TextureId);
        gl.TexImage2D(GLEnum.Texture2D, 0, (int)depthStencilTexture.InternalFormat.ToGlInternalFormat(), Width, Height, 0, depthStencilTexture.InternalFormat.ToGlPixelFormat(), depthStencilTexture.InternalFormat.ToGlPixelType(), (void*)0);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Nearest);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Nearest);


        gl.FramebufferTexture2D(GLEnum.Framebuffer, depthStencilTexture.InternalFormat.ToGlAttachment(), GLEnum.Texture2D, DepthStencilTexture.TextureId, 0);
        gl.DrawBuffers(ColorAttachmentSet);
        state = gl.CheckFramebufferStatus(GLEnum.Framebuffer);
    }

    public RenderTarget AddRenderTexture(string name, TextureFormat internalFormat)
    {
        renderTextures.Add(new RenderTexture(this)
        {
            InternalFormat = internalFormat
        });
        renderTexturesMap.Add(name, renderTextures.Last());

        return this;
    }


    public ITexture? GetTexture(int index)
    {
        if (index < 0)
            return null;
        if (index >= renderTextures.Count)
            return null;
        return renderTextures[index];
    }

    public ITexture GetTexture(string name)
    {
        if (renderTexturesMap.TryGetValue(name, out var texture))
        {
            return texture;
        }
        throw new KeyNotFoundException($"RenderTarget texture '{name}' not found");
    }


    public RenderTarget SetDepthTexture(TextureFormat textureFormat)
    {
        depthStencilTexture.InternalFormat = textureFormat;

        return this;
    }



    protected class RenderTexture : ITexture
    {

        public RenderTexture(RenderTarget rt)
        {
            RenderTarget = rt;
        }

        RenderTarget RenderTarget { get; set; }

        public uint TextureId { get; set; }

        public uint Width => RenderTarget.Width;

        public uint Height => RenderTarget.Height;

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

        _ => throw new NotImplementedException()
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

        _ => throw new NotImplementedException()

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
        _ => throw new NotImplementedException()
    };


    public static GLEnum ToGlAttachment(this TextureFormat format) => format switch
    {
        TextureFormat.DepthComponent16 => GLEnum.DepthAttachment,
        TextureFormat.DepthComponent24 => GLEnum.DepthAttachment,
        TextureFormat.DepthComponent32f => GLEnum.DepthAttachment,
        TextureFormat.Depth24Stencil8 => GLEnum.DepthStencilAttachment,
        TextureFormat.Depth32fStencil8 => GLEnum.DepthStencilAttachment,
        _ => throw new NotImplementedException()
    };
}