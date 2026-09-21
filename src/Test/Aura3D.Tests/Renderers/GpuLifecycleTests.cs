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
    public void InitializeAfterContextLost_ShouldAttachReplacementContext()
    {
        var pipeline = CreatePipeline();
        pipeline.Initialize(_ => 0);

        pipeline.HandleContextLost();

        Assert.False(pipeline.IsInitialized);

        pipeline.Initialize(_ => 0);

        Assert.True(pipeline.IsInitialized);
        Assert.False(pipeline.IsDestroyed);
    }

    [Fact]
    public void InitializeWithoutContextLoss_ShouldThrow()
    {
        var pipeline = CreatePipeline();
        pipeline.Initialize(_ => 0);

        var exception = Assert.Throws<InvalidOperationException>(() => pipeline.Initialize(_ => 0));

        Assert.Contains("context loss", exception.Message);
        Assert.True(pipeline.IsInitialized);
    }

    [Fact]
    public void InvalidatedState_ShouldReuploadOnReplacementContext()
    {
        var pipeline = CreatePipeline();
        var state = new FakeGpuState();
        pipeline.Initialize(_ => 0);
        pipeline.EnsureSynced(state);

        Assert.Equal(0, state.UploadCount);

        pipeline.HandleContextLost();

        Assert.Equal((ulong)0, state.SyncedVersion);

        pipeline.Initialize(_ => 0);
        pipeline.EnsureSynced(state);

        Assert.Equal(1, state.UploadCount);
        Assert.Equal((ulong)1, state.SyncedVersion);
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

    [Fact]
    public void ReleaseGpuResources_ShouldDestroyTrackedStateAndAllowReupload()
    {
        var pipeline = CreatePipeline();
        var state = new FakeGpuState();
        pipeline.Initialize(_ => 0);
        pipeline.EnsureSynced(state);

        pipeline.ReleaseGpuResources();

        Assert.Equal(1, state.DestroyCount);
        Assert.Equal(0, state.UploadCount);
        Assert.Equal((ulong)0, state.SyncedVersion);
        Assert.True(pipeline.IsInitialized);
        Assert.False(pipeline.IsDestroyed);

        pipeline.EnsureSynced(state);

        Assert.Equal(1, state.UploadCount);
        Assert.Equal((ulong)1, state.SyncedVersion);
    }

    [Fact]
    public void ReleaseGpuResourcesWithoutContext_ShouldOnlyInvalidate()
    {
        var pipeline = CreatePipeline();
        var state = new FakeGpuState();
        pipeline.EnsureSynced(state);

        pipeline.ReleaseGpuResources();

        Assert.Equal(1, state.InvalidateCount);
        Assert.Equal(0, state.DestroyCount);
        Assert.False(pipeline.IsDestroyed);
    }

    [Fact]
    public void ReleaseGpuResourcesAfterDestroy_ShouldThrow()
    {
        var pipeline = CreatePipeline();
        pipeline.Destroy();

        Assert.Throws<ObjectDisposedException>(() => pipeline.ReleaseGpuResources());
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
        public int UploadCount { get; private set; }

        public void Upload(GL gl)
        {
            UploadCount++;
            SyncedVersion = Version;
        }

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
