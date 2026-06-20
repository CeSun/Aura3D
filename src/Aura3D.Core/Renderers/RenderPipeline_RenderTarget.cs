using System.Drawing;

namespace Aura3D.Core.Renderers;

/// <summary>
/// 渲染管线的渲染目标管理部分，负责渲染目标的注册、缓存和回收。
/// </summary>
public abstract partial class RenderPipeline
{
    private readonly Dictionary<string, Dictionary<Size, (RenderTarget, DateTime)>> renderTargets = [];

    private readonly Dictionary<string, RenderTargetHandle> renderTargetHandles = [];

    private void UpdateRenderTargetsLRU()
    {
        foreach (var (name, rtMap) in renderTargets)
        {
            var expiredSizes = new List<Size>();
            foreach (var (rtSize, (rt, dateTime)) in rtMap)
            {
                if (DateTime.Now - dateTime > TimeSpan.FromSeconds(1))
                {
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
    /// 获取相机最终输出引用。
    /// </summary>
    protected internal RenderOutputRef CameraOutput => CameraOutputRef.Instance;

    /// <summary>
    /// 注册一个具有指定名称的渲染目标，并返回其引用与配置对象。
    /// </summary>
    /// <param name="name">渲染目标的名称。</param>
    /// <returns>渲染目标引用与配置对象。</returns>
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
    /// 获取指定名称和大小的渲染目标实例，若不存在则自动创建。
    /// </summary>
    /// <param name="name">渲染目标的名称。</param>
    /// <param name="size">渲染目标的尺寸。</param>
    /// <returns>渲染目标实例。</returns>
    /// <exception cref="KeyNotFoundException">当渲染目标未注册时抛出。</exception>
    internal RenderTarget GetRenderTarget(string name, Size size)
    {
        return GetRenderTarget(GetOrCreateRenderTargetHandle(name), size);
    }

    internal RenderTarget GetRenderTarget(RenderTargetHandle renderTargetHandle, Size size)
    {
        if (!ReferenceEquals(renderTargetHandle.OwnerPipeline, this))
        {
            throw new InvalidOperationException("Render target handle does not belong to the current render pipeline.");
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
                    .SetDepthTexture(rtConf.DepthTextureFormat), DateTime.Now);

                foreach (var (textureName, textureFormat) in rtConf.Textures)
                {
                    rt.Item1.AddRenderTexture(textureName, textureFormat);
                }
                EnsureUploaded(rt.Item1);
                rtMap.Add(size, rt);
            }
            else
            {
                rt.Item2 = DateTime.Now;
                rtMap[size] = rt;
            }
            return rt.Item1;
        }

        throw new KeyNotFoundException($"RenderTarget '{renderTargetHandle.Name}' not found. Ensure the render target is registered before use.");
    }
}
