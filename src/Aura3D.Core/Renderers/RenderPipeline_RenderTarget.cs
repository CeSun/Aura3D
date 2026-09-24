using System.Drawing;

namespace Aura3D.Core.Renderers;

/// <summary>
/// Represents the render pipeline type.
/// </summary>
public abstract partial class RenderPipeline
{
    private readonly Dictionary<string, Dictionary<Size, (RenderTarget, long)>> renderTargets = [];

    private readonly Dictionary<string, RenderTargetHandle> renderTargetHandles = [];

    /// <summary>
    /// Number of consecutive <see cref="Render"/> calls without a hit before a cached render target is reclaimed.
    /// Must stay frame based rather than wall-clock based: a slow frame would otherwise evict targets that are
    /// still in use every frame.
    /// </summary>
    private const int IdleRenderTargetReclaimRenders = 30;

    private long _renderSequence;

    private void ClearRenderTargetCaches()
    {
        renderTargets.Clear();
    }

    private void UpdateRenderTargetsLRU()
    {
        _renderSequence++;

        foreach (var (name, rtMap) in renderTargets)
        {
            var expiredSizes = new List<Size>();
            foreach (var (rtSize, (rt, lastUsedRender)) in rtMap)
            {
                if (_renderSequence - lastUsedRender > IdleRenderTargetReclaimRenders)
                {
                    RemoveGpuState(rt);
                    rt.Destroy(gl!);
                    expiredSizes.Add(rtSize);
                }
            }
            foreach (var rtSize in expiredSizes)
            {
                rtMap.Remove(rtSize);
            }
        }
    }

    /// <summary>
    /// Gets the camera output.
    /// </summary>
    protected internal RenderOutputRef CameraOutput => CameraOutputRef.Instance;

    /// <summary>
    /// Performs the register render target operation.
    /// </summary>
    protected RenderTargetHandle RegisterRenderTarget(string name)
    {
        return GetOrCreateRenderTargetHandle(name);
    }

    internal RenderTargetHandle GetOrCreateRenderTargetHandle(string name)
    {
        if (!renderTargetHandles.TryGetValue(name, out var renderTargetHandle))
        {
            renderTargetHandle = new RenderTargetHandle(this, name);
            renderTargetHandles.Add(name, renderTargetHandle);
        }

        return renderTargetHandle;
    }

    /// <summary>
    /// Gets the render target.
    /// </summary>
    internal RenderTarget GetRenderTarget(string name, Size size)
    {
        return GetRenderTarget(GetOrCreateRenderTargetHandle(name), size);
    }

    internal RenderTarget GetRenderTarget(RenderTargetHandle renderTargetHandle, Size size)
    {
        if (!ReferenceEquals(renderTargetHandle.OwnerPipeline, this))
        {
            throw Aura3D.Core.Exceptions.RendererErrors.RenderTargetOwnershipMismatch();
        }

        if (renderTargetHandles.TryGetValue(renderTargetHandle.Name, out var rtConf))
        {
            if (renderTargets.TryGetValue(renderTargetHandle.Name, out var rtMap) == false)
            {
                rtMap = [];
                renderTargets.Add(renderTargetHandle.Name, rtMap);
            }

            if (rtMap.TryGetValue(size, out var rt) == false)
            {
                rt = (new RenderTarget()
                    .SetSize((uint)size.Width, (uint)size.Height)
                    .SetDepthTexture(rtConf.DepthTextureFormat), _renderSequence);

                foreach (var (textureName, textureFormat) in rtConf.Textures)
                {
                    rt.Item1.AddRenderTexture(textureName, textureFormat);
                }
                EnsureSynced(rt.Item1);
                rtMap.Add(size, rt);
            }
            else
            {
                rt.Item2 = _renderSequence;
                rtMap[size] = rt;
            }
            EnsureSynced(rt.Item1);
            return rt.Item1;
        }

        throw Aura3D.Core.Exceptions.RendererErrors.RenderTargetNotFound(renderTargetHandle.Name);
    }
}
