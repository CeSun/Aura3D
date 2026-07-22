using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;

namespace Aura3D.Core.Renderers;

public class CubeRenderTarget : RenderTargetBase<RenderCubeTexture, CubeRenderTarget>
{
    public CubeRenderTarget()
    {
        depthStencilTexture = new RenderCubeTexture(this);
    }

    public override CubeRenderTarget AddRenderTexture(string name, TextureFormat internalFormat)
    {
        renderTextures.Add(new RenderCubeTexture(this)
        {
            InternalFormat = internalFormat
        });
        renderTexturesMap.Add(name, renderTextures[^1]);
        Version++;

        return this;
    }


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

            if (EnableMipMap)
                gl.GenerateMipmap(GLEnum.TextureCubeMap);

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
            throw Aura3D.Core.Exceptions.RendererErrors.FramebufferCreationFailed(state, nameof(CubeRenderTarget));
        }

        SyncTextureSizes();
        SyncedVersion = Version;
    }
}


/// <summary>
/// 渲染目标内部的 Cube 纹理实现类。
/// </summary>
public sealed class RenderCubeTexture : CubeTexture, IRenderTargetTexture
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
