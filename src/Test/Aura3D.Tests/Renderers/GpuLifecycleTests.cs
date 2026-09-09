using Aura3D.Core.Renderers;
using Aura3D.Core.Scenes;
using Silk.NET.OpenGLES;
using Xunit;

namespace Aura3D.Tests.Renderers;

public class GpuLifecycleTests
{
    [Fact]
    public void HandleContextLost_ShouldInvalidateTrackedStateWithoutDestroyingIt()
    {
        var pipeline = CreatePipeline();
        var state = new FakeGpuState();
        pipeline.EnsureSynced(state);

        pipeline.HandleContextLost();

        Assert.Equal(1, state.InvalidateCount);
        Assert.Equal(0, state.DestroyCount);
        Assert.False(pipeline.IsInitialized);
        Assert.False(pipeline.IsDestroyed);
    }

    [Fact]
    public void DestroyWithoutContext_ShouldInvalidateOnceAndBeIdempotent()
    {
        var pipeline = CreatePipeline();
        var state = new FakeGpuState();
        pipeline.EnsureSynced(state);

        pipeline.Destroy();
        pipeline.Destroy();

        Assert.Equal(1, state.InvalidateCount);
        Assert.Equal(0, state.DestroyCount);
        Assert.True(pipeline.IsDestroyed);
        Assert.Throws<ObjectDisposedException>(() => pipeline.EnsureSynced(state));
    }

    [Fact]
    public void RenderTargetInvalidate_ShouldResetOwnedNamesWithoutGlCalls()
    {
        var target = new RenderTarget().SetSize(16, 16).AddRenderTexture("Color", TextureFormat.Rgba8);
        target.FrameBufferId = 7;
        target.GetTexture(0)!.TextureId = 8;
        target.DepthStencilTexture.TextureId = 9;

        target.Invalidate();
        target.Invalidate();

        Assert.Equal((uint)0, target.FrameBufferId);
        Assert.Equal((uint)0, target.GetTexture(0)!.TextureId);
        Assert.Equal((uint)0, target.DepthStencilTexture.TextureId);
    }

    private static TestPipeline CreatePipeline()
    {
        TestPipeline? pipeline = null;
        _ = new Scene(scene => pipeline = new TestPipeline(scene));
        return pipeline!;
    }

    private sealed class TestPipeline(Scene scene) : RenderPipeline(scene);

    private sealed class FakeGpuState : IRuntimeGpuState
    {
        public ulong Version => 1;
        public ulong SyncedVersion { get; private set; } = 1;
        public int DestroyCount { get; private set; }
        public int InvalidateCount { get; private set; }

        public void Upload(GL gl) => SyncedVersion = Version;

        public void Destroy(GL gl)
        {
            DestroyCount++;
            Invalidate();
        }

        public void Invalidate()
        {
            InvalidateCount++;
            SyncedVersion = 0;
        }
    }
}
