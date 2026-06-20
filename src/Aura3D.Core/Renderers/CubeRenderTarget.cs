using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;

namespace Aura3D.Core.Renderers;

public class CubeRenderTarget : IRenderTarget
{

    public CubeRenderTarget()
    {
        depthStencilTexture = new RenderCubeTexture(this);
    }


    protected List<RenderCubeTexture> renderTextures = new List<RenderCubeTexture>();

    protected Dictionary<string, RenderCubeTexture> renderTexturesMap = new Dictionary<string, RenderCubeTexture>();

    protected RenderCubeTexture depthStencilTexture;

    public RenderCubeTexture DepthStencilTexture => depthStencilTexture;

    public bool NeedsUpload { get; set; } = false;

    public uint FrameBufferId { get; set; }

    public uint Height { get; set; }

    public uint Width { get; set; }

    public float Scale { get; set; } = 1.0f;

    public bool EnableMipMap 
    { 
        get; 
        set; 
    }


    public CubeRenderTarget SetEnableMipMapLevel(bool enableMipMap)
    {
        EnableMipMap = enableMipMap;

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
        if (depthStencilTexture.TextureId != 0)
            gl.DeleteTexture(depthStencilTexture.TextureId);

        if (FrameBufferId != 0)
        {
            gl.DeleteFramebuffer(FrameBufferId);
        }
    }


    public CubeRenderTarget SetSize(uint width, uint height)
    {
        Width = width;
        Height = height;
        SyncTextureSizes();
        NeedsUpload = true;
        return this;
    }

    public CubeRenderTarget AddRenderTexture(string name, TextureFormat internalFormat)
    {
        renderTextures.Add(new RenderCubeTexture(this)
        {
            InternalFormat = internalFormat
        });
        renderTexturesMap.Add(name, renderTextures.Last());

        return this;
    }


    public RenderCubeTexture? GetTexture(int index)
    {
        if (index < 0)
            return null;
        if (index >= renderTextures.Count)
            return null;
        return renderTextures[index];
    }

    public RenderCubeTexture? GetTexture(string name)
    {
        if (renderTexturesMap.TryGetValue(name, out var texture))
        {
            return texture;
        }
        return null;
    }


    public CubeRenderTarget SetDepthTexture(TextureFormat textureFormat)
    {
        depthStencilTexture.InternalFormat = textureFormat;

        return this;
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

            gl.BindTexture(GLEnum.TextureCubeMap, texture.TextureId);
            


            for (int i = 0; i < 6; i++)
            {
                gl.TexImage2D((GLEnum)((uint)GLEnum.TextureCubeMapPositiveX + i), 0, (int)texture.InternalFormat.ToGlInternalFormat(), (uint)Width, (uint)Height, 0, (GLEnum)texture.InternalFormat.ToGlPixelFormat(), (GLEnum)texture.InternalFormat.ToGlPixelType(), null);

            }

            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureMinFilter, (int)GLEnum.Linear);
            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureMagFilter, (int)GLEnum.Linear);

            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);
            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapR, (int)GLEnum.ClampToEdge);


            gl.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.ColorAttachment0 + index, GLEnum.TextureCubeMapPositiveX, texture.TextureId, 0);
            ColorAttachmentSet[index] = GLEnum.ColorAttachment0 + index;
            index++;
        }

        depthStencilTexture.TextureId = gl.GenTexture();
        gl.BindTexture(GLEnum.TextureCubeMap, depthStencilTexture.TextureId);
        for (int i = 0; i < 6; i++)
        {
            gl.TexImage2D(GLEnum.TextureCubeMapPositiveX + i, 0, (int)depthStencilTexture.InternalFormat.ToGlInternalFormat(), (uint)Width, (uint)Height, 0, depthStencilTexture.InternalFormat.ToGlPixelFormat(), depthStencilTexture.InternalFormat.ToGlPixelType(), (void*)0);

        }
      

        gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureMinFilter, (int)GLEnum.Linear);
        gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureMagFilter, (int)GLEnum.Linear);

        gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);
        gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapR, (int)GLEnum.ClampToEdge);
        gl.FramebufferTexture2D(GLEnum.Framebuffer, depthStencilTexture.InternalFormat.ToGlAttachment(), GLEnum.TextureCubeMapPositiveX, depthStencilTexture.TextureId, 0);
        gl.DrawBuffers(ColorAttachmentSet);
        state = gl.CheckFramebufferStatus(GLEnum.Framebuffer);

        if (state != GLEnum.FramebufferComplete)
        {
            throw new Exception("create framebuffer error: " + state);
        }

        SyncTextureSizes();
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

    public sealed class RenderCubeTexture : CubeTexture
    {

        public RenderCubeTexture(CubeRenderTarget rt)
        {
            RenderTarget = rt;
            WrapS = Aura3D.Core.Resources.TextureWrapMode.ClampToEdge;
            WrapT = Aura3D.Core.Resources.TextureWrapMode.ClampToEdge;
            WrapR = Aura3D.Core.Resources.TextureWrapMode.ClampToEdge;
            MinFilter = Aura3D.Core.Resources.TextureFilterMode.Linear;
            MagFilter = Aura3D.Core.Resources.TextureFilterMode.Linear;
        }

        CubeRenderTarget RenderTarget { get; set; }

        internal CubeTextureGpuState? CachedGpuState { get; set; }

        public uint TextureId { get; set; }

        public TextureFormat InternalFormat { get; set; }
    }
}
