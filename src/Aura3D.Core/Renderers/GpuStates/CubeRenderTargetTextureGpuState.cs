using Silk.NET.OpenGLES;

namespace Aura3D.Core.Renderers;

internal sealed class CubeRenderTargetTextureGpuState : CubeTextureGpuState
{
    public CubeRenderTargetTextureGpuState(CubeRenderTarget.RenderCubeTexture texture)
        : base(texture)
    {
    }

    public override uint TextureId
    {
        get => RenderTexture.TextureId;
        protected set => RenderTexture.TextureId = value;
    }

    private CubeRenderTarget.RenderCubeTexture RenderTexture => (CubeRenderTarget.RenderCubeTexture)GetResource();

    public override void Destroy(GL gl)
    {
    }

    public override void Upload(GL gl)
    {
        SyncedVersion = RenderTexture.Version;
    }
}
