using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;

namespace Aura3D.Core.Renderers;

internal sealed class WritableTextureGpuState : TextureGpuState
{
    private readonly TextureFormat depthFormat;

    public uint FramebufferId { get; private set; }

    public uint DepthTextureId { get; private set; }

    public WritableTextureGpuState(WritableTexture texture, TextureFormat depthFormat)
        : base(texture)
    {
        this.depthFormat = depthFormat;
    }

    public override void Destroy(GL gl)
    {
        DestroyFramebuffer(gl);
        base.Destroy(gl);
    }

    public override unsafe void Upload(GL gl)
    {
        DestroyFramebuffer(gl);
        base.Upload(gl);

        var texture = (WritableTexture)GetResource();
        FramebufferId = gl.GenFramebuffer();
        gl.BindFramebuffer(GLEnum.Framebuffer, FramebufferId);
        gl.FramebufferTexture2D(
            GLEnum.Framebuffer,
            GLEnum.ColorAttachment0,
            GLEnum.Texture2D,
            TextureId,
            0);

        DepthTextureId = gl.GenTexture();
        gl.BindTexture(GLEnum.Texture2D, DepthTextureId);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Nearest);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Nearest);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);
        gl.TexImage2D(
            GLEnum.Texture2D,
            0,
            (int)depthFormat.ToGlInternalFormat(),
            texture.Width,
            texture.Height,
            0,
            depthFormat.ToGlPixelFormat(),
            depthFormat.ToGlPixelType(),
            null);
        gl.FramebufferTexture2D(
            GLEnum.Framebuffer,
            depthFormat.ToGlAttachment(),
            GLEnum.Texture2D,
            DepthTextureId,
            0);

        Span<GLEnum> colorAttachments = stackalloc GLEnum[] { GLEnum.ColorAttachment0 };
        gl.DrawBuffers(colorAttachments);

        var status = gl.CheckFramebufferStatus(GLEnum.Framebuffer);
        if (status != GLEnum.FramebufferComplete)
        {
            throw Aura3D.Core.Exceptions.RendererErrors.FramebufferCreationFailed(status, nameof(WritableTexture));
        }

        gl.BindTexture(GLEnum.Texture2D, 0);
        gl.BindFramebuffer(GLEnum.Framebuffer, 0);
    }

    protected override unsafe void UploadTextureStorage(GL gl, Aura3D.Core.Resources.Texture texture)
    {
        var writableTexture = (WritableTexture)texture;
        gl.TexImage2D(
            GLEnum.Texture2D,
            0,
            writableTexture.Format.ToGlInternalFormat(),
            writableTexture.Width,
            writableTexture.Height,
            0,
            writableTexture.Format.ToGlPixelFormat(),
            writableTexture.Format.ToGlPixelType(),
            null);
    }

    private void DestroyFramebuffer(GL gl)
    {
        if (DepthTextureId != 0)
        {
            gl.DeleteTexture(DepthTextureId);
            DepthTextureId = 0;
        }

        if (FramebufferId != 0)
        {
            gl.DeleteFramebuffer(FramebufferId);
            FramebufferId = 0;
        }
    }
}
