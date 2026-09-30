using Aura3D.Core;
using Aura3D.Core.Nodes;
using Aura3D.Model;
using Aura3D.Core.Resources;
using Aura3D.Gallery.Assets;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Aura3D.Gallery;

/// <summary>
/// 一页演示所需资产的批量取用入口：负责上报进度、缓存已解码结果，
/// 并把「取字节 → 解码」这条固定流程收敛成一行调用，避免每个演示各写一份。
/// 所有解码都排到线程池执行——浏览器端 CPU 解一张 4K 图要几十秒，不能堵 UI 线程。
/// </summary>
public sealed class AssetBatch
{
    private readonly IAssetProvider provider;
    private readonly List<AssetRef> remaining;
    private int completed;

    internal event Action<AssetProgress>? Progress;

    internal AssetBatch(IAssetProvider provider, AssetSet set)
    {
        this.provider = provider;

        remaining = set.Items.Where(i => i.Bytes > 0).ToList();
    }

    /// <summary>本次取用的资产总数。</summary>
    public int TotalAssets => remaining.Count;

    /// <summary>已完成的资产数。</summary>
    public int CompletedAssets => completed;

    /// <summary>
    /// 打开一个资产的原始字节流。已解码过的项目仍会重新取流（解码结果请用下面的强类型 helper）。
    /// </summary>
    /// <param name="key">清单里的 Key。</param>
    public async Task<Stream> OpenAsync(string key)
    {
        var asset = remaining.FirstOrDefault(a => a.Key == key);

        if (asset.Key == null)
            asset = AssetManifest.All.First(a => a.Key == key);

        var progress = new Progress<AssetProgress>(p =>
            Progress?.Invoke(p with { CompletedAssets = completed, TotalAssets = remaining.Count }));

        var stream = await provider.OpenAsync(asset, progress);

        completed++;

        Progress?.Invoke(new AssetProgress(asset.Key, asset.Bytes, asset.Bytes, completed, remaining.Count));

        return stream;
    }

    /// <summary>
    /// 解码一张纹理（PNG/JPG/HDR 由 <see cref="TextureLoader"/> 判定）。结果按 Key 缓存。
    /// </summary>
    public Task<Texture> TextureAsync(string key) =>
        DecodeAsync(key, stream => TextureLoader.LoadTexture(stream));

    /// <summary>
    /// 解码一张 HDRI 纹理，用于 <see cref="HDRIToCubeTextureConverter"/> 或直接作环境贴图。
    /// </summary>
    public Task<Texture> HdrTextureAsync(string key) =>
        DecodeAsync(key, stream => TextureLoader.LoadHdrTexture(stream));

    /// <summary>
    /// 按清单里 <see cref="AssetManifest.SkyboxKeys"/> 的顺序解码六面立方图。
    /// </summary>
    public async Task<CubeTexture> CubeTextureAsync()
    {
        var streams = new List<Stream>();

        try
        {
            foreach (var key in AssetManifest.SkyboxKeys)
            {
                streams.Add(await OpenAsync(key));
            }

            return await Task.Run(() => TextureLoader.LoadCubeTexture(streams));
        }
        finally
        {
            foreach (var stream in streams)
                stream.Dispose();
        }
    }

    /// <summary>
    /// 加载 glTF/GLB 模型（不含动画）。
    /// </summary>
    public Task<Core.Nodes.Model> ModelAsync(string key) =>
        DecodeAsync(key, stream => Aura3D.Model.ModelLoader.LoadGlbModel(stream));

    /// <summary>
    /// 加载 glTF/GLB 模型及其骨骼动画。
    /// </summary>
    public Task<(Core.Nodes.Model Model, List<Animation> Animations)> ModelWithAnimationsAsync(string key) =>
        DecodeAsync(key, stream =>
        {
            var (model, animations) = Aura3D.Model.ModelLoader.LoadGlbModelAndAnimations(stream);

            return (model, animations);
        });

    /// <summary>
    /// 用 Assimp 加载任意其支持的模型（FBX/OBJ/…）。需要原生库，浏览器端不可用。
    /// </summary>
    public Task<Core.Nodes.Model> AssimpModelAsync(string key, Func<string, Texture>? loadTextureFunc = null) =>
        DecodeAsync(key, stream =>
        {
            var extension = Path.GetExtension(AssetManifest.All.First(a => a.Key == key).Path);

            return AssimpLoader.Load(stream, extension, loadTextureFunc);
        });

    /// <summary>
    /// 用 Assimp 加载动画片段集合。<paramref name="skeleton"/> 传已加载模型的骨骼，
    /// 这样片段可以直接挂到同一角色上（FBX 动作库的标准用法）。
    /// </summary>
    public Task<List<Animation>> AssimpAnimationsAsync(string key, Skeleton? skeleton) =>
        DecodeAsync(key, stream =>
        {
            var extension = Path.GetExtension(AssetManifest.All.First(a => a.Key == key).Path);

            return AssimpLoader.LoadAnimations(stream, skeleton, extension);
        });

    /// <summary>
    /// 读出一个文本资产（清单/调试用）。
    /// </summary>
    public async Task<string> TextAsync(string key)
    {
        using var stream = await OpenAsync(key);
        using var reader = new StreamReader(stream);

        return await reader.ReadToEndAsync();
    }

    private async Task<T> DecodeAsync<T>(string key, Func<Stream, T> decode)
    {
        if (decoded.TryGetValue(key, out var cached))
            return (T)cached;

        using var stream = await OpenAsync(key);

        // 先把字节复制一份：using 的流会在解码任务排队期间就被释放，
        // 而解码要在线程池上跑几十秒（浏览器端 CPU 解 HDRI 尤其慢）。
        var copy = new MemoryStream();

        await stream.CopyToAsync(copy);

        copy.Position = 0;

        var result = await Task.Run(() => decode(copy));

        decoded[key] = result!;

        return result;
    }

    private readonly ConcurrentDictionary<string, object> decoded = new(StringComparer.OrdinalIgnoreCase);
}
