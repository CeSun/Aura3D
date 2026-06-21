using Silk.NET.OpenGLES;

namespace Aura3D.Core.Renderers;

internal sealed class RenderTargetTextureGpuState : TextureGpuState
{
    public RenderTargetTextureGpuState(RenderTarget.RenderTexture texture)
        : base(texture)
    {
    }

    public override uint TextureId
    {
        get => RenderTexture.TextureId;
        protected set => RenderTexture.TextureId = value;
    }

    private RenderTarget.RenderTexture RenderTexture => (RenderTarget.RenderTexture)GetResource();

    public override void Destroy(GL gl)
    {
    }

    public override void Upload(GL gl)
    {
        SyncedVersion = RenderTexture.Version;
    }
}
