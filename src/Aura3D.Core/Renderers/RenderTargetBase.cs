using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;

namespace Aura3D.Core.Renderers;

/// <summary>
/// 渲染目标内部纹理附件的统一契约，供 <see cref="RenderTargetBase{TTexture, TSelf}"/> 在 2D / Cube 两种实现间共享存储与生命周期管理。
/// </summary>
public interface IRenderTargetTexture
{
    /// <summary>OpenGL 纹理对象 ID。</summary>
    uint TextureId { get; set; }

    /// <summary>纹理内部格式。</summary>
    TextureFormat InternalFormat { get; set; }

    /// <summary>纹理宽度。</summary>
    uint Width { get; set; }

    /// <summary>纹理高度。</summary>
    uint Height { get; set; }
}

/// <summary>
/// 渲染目标基类，承载 2D / Cube 渲染目标共享的字段、集合与生命周期管理。
/// 派生类通过 CRTP 指定具体纹理类型与自身类型，以保持 fluent API 的具体类型返回。
/// </summary>
/// <typeparam name="TTexture">渲染目标内部纹理附件类型，必须实现 <see cref="IRenderTargetTexture"/>。</typeparam>
/// <typeparam name="TSelf">派生类自身类型，用于 fluent API 返回具体类型。</typeparam>
public abstract class RenderTargetBase<TTexture, TSelf> : IRuntimeGpuState
    where TTexture : class, IRenderTargetTexture
    where TSelf : RenderTargetBase<TTexture, TSelf>
{
    /// <inheritdoc />
    public ulong Version { get; protected set; } = 1;

    /// <inheritdoc />
    public ulong SyncedVersion { get; protected set; }

    /// <summary>颜色附件列表，按 <see cref="AddRenderTexture"/> 添加顺序排列。</summary>
    protected List<TTexture> renderTextures = new();

    /// <summary>颜色附件名称到纹理的映射，供 <see cref="GetTexture(string)"/> 查询。</summary>
    protected Dictionary<string, TTexture> renderTexturesMap = new();

    /// <summary>深度/模板附件纹理，由派生类构造函数初始化。</summary>
    protected TTexture depthStencilTexture = default!;

    /// <summary>获取深度/模板附件纹理。</summary>
    public TTexture DepthStencilTexture => depthStencilTexture;

    /// <summary>获取或设置帧缓冲对象的 ID。</summary>
    public uint FrameBufferId { get; set; }

    /// <summary>获取或设置渲染目标的高度。</summary>
    public uint Height { get; set; }

    /// <summary>获取或设置渲染目标的宽度。</summary>
    public uint Width { get; set; }

    /// <inheritdoc />
    public float Scale { get; set; } = 1.0f;

    /// <summary>是否启用 mipmap 生成。</summary>
    public bool EnableMipMap { get; set; }

    /// <summary>获取深度/模板附件的纹理格式。</summary>
    public TextureFormat DepthTextureFormat { get; protected set; }

    /// <summary>
    /// 启用或禁用 mipmap 生成。
    /// </summary>
    /// <param name="enableMipMap">是否启用 mipmap。</param>
    /// <returns>当前的渲染目标实例。</returns>
    public TSelf SetEnableMipMapLevel(bool enableMipMap)
    {
        if (EnableMipMap == enableMipMap)
            return (TSelf)this;
        EnableMipMap = enableMipMap;
        Version++;
        return (TSelf)this;
    }

    /// <summary>
    /// 设置渲染目标的尺寸。
    /// </summary>
    /// <param name="width">宽度。</param>
    /// <param name="height">高度。</param>
    /// <returns>当前的渲染目标实例。</returns>
    public TSelf SetSize(uint width, uint height)
    {
        if (Width == width && Height == height)
            return (TSelf)this;

        Width = width;
        Height = height;
        SyncTextureSizes();
        Version++;
        return (TSelf)this;
    }

    /// <summary>
    /// 获取指定索引的颜色纹理。
    /// </summary>
    /// <param name="index">纹理索引。</param>
    /// <returns>纹理实例；索引无效时返回 <c>null</c>。</returns>
    public TTexture? GetTexture(int index)
    {
        if (index < 0)
            return null;
        if (index >= renderTextures.Count)
            return null;
        return renderTextures[index];
    }

    /// <summary>
    /// 获取指定名称的颜色纹理。
    /// </summary>
    /// <param name="name">纹理名称。</param>
    /// <returns>纹理实例；不存在时返回 <c>null</c>。</returns>
    public TTexture? GetTexture(string name)
    {
        if (renderTexturesMap.TryGetValue(name, out var texture))
            return texture;
        return null;
    }

    /// <summary>
    /// 设置深度/模板纹理格式。
    /// </summary>
    /// <param name="textureFormat">深度纹理格式。</param>
    /// <returns>当前的渲染目标实例。</returns>
    public TSelf SetDepthTexture(TextureFormat textureFormat)
    {
        if (DepthTextureFormat == textureFormat)
            return (TSelf)this;

        depthStencilTexture.InternalFormat = textureFormat;
        DepthTextureFormat = textureFormat;
        Version++;
        return (TSelf)this;
    }

    /// <summary>
    /// 向渲染目标添加一个颜色纹理。
    /// </summary>
    /// <param name="name">纹理名称。</param>
    /// <param name="internalFormat">纹理内部格式。</param>
    /// <returns>当前的渲染目标实例。</returns>
    public abstract TSelf AddRenderTexture(string name, TextureFormat internalFormat);

    /// <summary>
    /// 同步所有附件纹理的尺寸到当前 <see cref="Width"/> / <see cref="Height"/>。
    /// </summary>
    protected void SyncTextureSizes()
    {
        foreach (var texture in renderTextures)
        {
            texture.Width = Width;
            texture.Height = Height;
        }

        depthStencilTexture.Width = Width;
        depthStencilTexture.Height = Height;
    }

    /// <summary>
    /// 销毁渲染目标及其关联的所有 GPU 资源。
    /// </summary>
    /// <param name="gl">OpenGL 上下文。</param>
    public void Destroy(GL gl)
    {
        foreach (var texture in renderTextures)
        {
            if (texture.TextureId != 0)
            {
                gl.DeleteTexture(texture.TextureId);
                texture.TextureId = 0;
            }
        }
        if (depthStencilTexture.TextureId != 0)
            gl.DeleteTexture(depthStencilTexture.TextureId);

        if (FrameBufferId != 0)
        {
            gl.DeleteFramebuffer(FrameBufferId);
        }

        SyncedVersion = 0;
    }

    /// <summary>
    /// 上传渲染目标数据到 GPU。
    /// </summary>
    /// <param name="gl">OpenGL 上下文。</param>
    public abstract void Upload(GL gl);
}
