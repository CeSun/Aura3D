using Silk.NET.OpenGLES;

namespace Aura3D.Core.Renderers;

internal sealed class CubeRenderTargetTextureGpuState : CubeTextureGpuState
{
    public CubeRenderTargetTextureGpuState(RenderCubeTexture texture)
        : base(texture)
    {
    }

    public override uint TextureId
    {
        get => RenderTexture.TextureId;
        protected set => RenderTexture.TextureId = value;
    }

    private RenderCubeTexture RenderTexture => (RenderCubeTexture)GetResource();

    public override void Destroy(GL gl)
    {
        Invalidate();
    }

    public override void Invalidate()
    {
        // The render target owns the texture name; this adapter only caches sync state.
        SyncedVersion = 0;
    }

    public override void Upload(GL gl)
    {
        SyncedVersion = RenderTexture.Version;
    }
}
