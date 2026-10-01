using Aura3D.Core;
using Aura3D.Core.Nodes;
using Aura3D.Core.Renderers;
using Aura3D.Core.Resources;
using Aura3D.Core.Scenes;
using System.Drawing;

namespace Aura3D.Pipeline.PBR.Common;

public abstract class PBRPipelineBase : RenderPipeline
{
    /// <summary>
    /// 相机上存辐照度图的 GPU 状态键。
    /// </summary>
    public const string IrradianceMapStateKey = "IrradianceMap";

    /// <summary>
    /// 相机上存预滤波反射图的 GPU 状态键。
    /// </summary>
    public const string PrefilteredEnvironmentMapStateKey = "PrefilteredEnvironmentMap";

    public Texture DefaultBaseColor { get; private set; }

    public Texture DefaultNormal { get; private set; }

    public Texture DefaultMetallicRoughness { get; private set; }

    public Texture DefaultEmissive { get; private set; }

    public Texture DefaultOcclusion { get; private set; }

    public Texture BrdfLutTexture { get; }

    public CubeTexture DefaultIblAmbientCubeTexture
    {
        get
        {
            if (_defaultIblAmbientCubeTexture == null)
            {
                var texture = Texture.CreateFromColor(Color.White);
                var cube = HDRIToCubeTextureConverter.ConvertFromTexture(texture, 16);
                _defaultIblAmbientCubeTexture = cube;
                EnsureSynced(cube);
            }

            return _defaultIblAmbientCubeTexture;
        }
    }

    private CubeTexture? _defaultIblAmbientCubeTexture;

    protected PBRPipelineBase(Scene scene) : base(scene)
    {
        using (var ms = new MemoryStream(PbrCommonResources.LutData))
        {
            BrdfLutTexture = Core.TextureLoader.LoadHdrTexture(ms);
        }

        DefaultBaseColor = Texture.CreateFromColor(Color.White);
        DefaultNormal = Texture.CreateFromColor(Color.FromArgb(128, 128, 255));
        DefaultMetallicRoughness = Texture.CreateFromColor(Color.FromArgb(0, 127, 0));
        DefaultEmissive = Texture.CreateFromColor(Color.Black);
        DefaultOcclusion = Texture.CreateFromColor(Color.White);
    }

    public override void Setup()
    {
        if (gl == null)
            return;

        EnsureSynced(DefaultBaseColor);
        EnsureSynced(DefaultNormal);
        EnsureSynced(DefaultMetallicRoughness);
        EnsureSynced(DefaultEmissive);
        EnsureSynced(DefaultOcclusion);
        EnsureSynced(BrdfLutTexture);
    }

    /// <summary>
    /// 背景立方图是 IBL 烘焙的唯一输入，换背景就得把两张烘焙图重算。
    /// 帧循环是 Update → Render → SceneUpdated：首帧的烘焙跑在页面的 BuildScene 之前，
    /// 那时背景还是 <see cref="DefaultIblAmbientCubeTexture"/> 那张纯白立方图，
    /// 而烘焙 pass 以 FrameBufferId != 0 判定"已烘过"直接早退——不在这里作废的话，
    /// 页面之后挂上的天空盒永远进不了 IBL，金属反射会一直是均匀白（看起来像没有金属度）。
    /// </summary>
    public override void OnBackgroundChanged()
    {
        InvalidateIblBakeCaches();
    }

    /// <summary>
    /// 把每个相机上烘好的辐照度图与预滤波反射图标记为失效，下一帧用当前背景重烘。
    /// 换背景已由 <see cref="Scene.Background"/> 的 setter 自动触发；手动调用用于
    /// 背景资源本身没换、但内容变了（例如同一张立方图被重新填充）的场合。
    /// </summary>
    public void InvalidateIblBakeCaches()
    {
        foreach (var camera in Cameras)
            InvalidateIblBakeCaches(camera);
    }

    /// <summary>
    /// 作废指定相机上的两张 IBL 烘焙图。
    /// </summary>
    public void InvalidateIblBakeCaches(Camera camera)
    {
        ArgumentNullException.ThrowIfNull(camera);

        foreach (var key in new[] { IrradianceMapStateKey, PrefilteredEnvironmentMapStateKey })
        {
            if (camera.GetPipelineGpuState<CubeRenderTarget>(key) is { } target)
                target.Invalidate();
        }
    }
}
