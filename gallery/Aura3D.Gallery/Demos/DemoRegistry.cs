using Aura3D.Gallery.Demos;
using Aura3D.Gallery.Localization;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aura3D.Gallery;

/// <summary>
/// 全部功能页的目录。导航、深链、体积预算都读这张表，所以新增一页只需在这里加一项。
/// 顺序即导航顺序，按分组聚集。
/// </summary>
public static class DemoRegistry
{
    /// <summary>
    /// 功能页目录。
    /// </summary>
    public static IReadOnlyList<DemoDescriptor> All { get; } =
    [
        new(
            Id: "geometries",
            Title: Strings.Keys.Demo_Geometries_Title,
            Group: Strings.Keys.Group_Basics,
            Summary: Strings.Keys.Demo_Geometries_Summary,
            Assets: Assets.AssetSet.Empty,
            Create: context => new GeometriesDemo(context)),
        new(
            Id: "pipelines",
            Title: Strings.Keys.Demo_Pipelines_Title,
            Group: Strings.Keys.Group_Pipelines,
            Summary: Strings.Keys.Demo_Pipelines_Summary,
            Assets: Assets.AssetSet.Empty,
            Create: context => new PipelinesDemo(context)),
        new(
            Id: "primitives",
            Title: Strings.Keys.Demo_Primitives_Title,
            Group: Strings.Keys.Group_Basics,
            Summary: Strings.Keys.Demo_Primitives_Summary,
            Assets: Assets.AssetSet.Empty,
            Create: context => new PrimitivesDemo(context),
            DefaultPipeline: PipelineKind.NoLight,
            // 顶点色/点精灵的着色器只覆盖了 NoLightPass 的类名，切到 LightPass 会得到引擎默认着色器。
            LockPipeline: true),

        new(
            Id: "camera",
            Title: Strings.Keys.Demo_Camera_Title,
            Group: Strings.Keys.Group_Basics,
            Summary: Strings.Keys.Demo_Camera_Summary,
            Assets: Assets.AssetSet.Empty,
            Create: context => new CameraDemo(context)),
        new(
            Id: "material-shaders",
            Title: Strings.Keys.Demo_MaterialShaders_Title,
            Group: Strings.Keys.Group_Materials,
            Summary: Strings.Keys.Demo_MaterialShaders_Summary,
            Assets: Assets.AssetSet.Empty,
            Create: context => new MaterialShadersDemo(context),
            // 覆盖键写死为 LightPass，换管线就没有覆盖可言了。
            LockPipeline: true),
        new(
            Id: "pbr-channels",
            Title: Strings.Keys.Demo_PbrChannels_Title,
            Group: Strings.Keys.Group_Materials,
            Summary: Strings.Keys.Demo_PbrChannels_Summary,
            Assets: Assets.AssetSet.Empty,
            Create: context => new PbrChannelsDemo(context),
            DefaultPipeline: PipelineKind.PBRForward),
        new(
            Id: "cel-shading",
            Title: Strings.Keys.Demo_CelShading_Title,
            Group: Strings.Keys.Group_Materials,
            Summary: Strings.Keys.Demo_CelShading_Summary,
            Assets: Assets.AssetSet.Empty,
            Create: context => new CelShadingDemo(context),
            DefaultPipeline: PipelineKind.CelShading,
            // 材质参数名与通道名只有 CelLightPass 认，切到别的管线就一个都不生效了。
            LockPipeline: true),
        new(
            Id: "ibl-environment",
            Title: Strings.Keys.Demo_IblEnvironment_Title,
            Group: Strings.Keys.Group_Pipelines,
            Summary: Strings.Keys.Demo_IblEnvironment_Summary,
            Assets: Assets.AssetSet.Empty,
            Create: context => new EnvironmentDemo(context),
            DefaultPipeline: PipelineKind.PBRForward),

        new(
            Id: "skybox-background",
            Title: Strings.Keys.Demo_SkyboxBackground_Title,
            Group: Strings.Keys.Group_Pipelines,
            Summary: Strings.Keys.Demo_SkyboxBackground_Summary,
            Assets: Assets.AssetManifest.RequireSet(
                "SkyboxPx", "SkyboxNx", "SkyboxPy", "SkyboxNy", "SkyboxPz", "SkyboxNz", "Hdr1k", "BackgroundJpg"),
            Create: context => new SkyboxBackgroundDemo(context),
            DefaultPipeline: PipelineKind.PBRForward),

        new(
            Id: "shadows",
            Title: Strings.Keys.Demo_Shadows_Title,
            Group: Strings.Keys.Group_Pipelines,
            Summary: Strings.Keys.Demo_Shadows_Summary,
            Assets: Assets.AssetSet.Empty,
            Create: context => new ShadowsDemo(context)),

        new(
            Id: "scene-gizmos",
            Title: Strings.Keys.Demo_SceneGizmos_Title,
            Group: Strings.Keys.Group_Scene,
            Summary: Strings.Keys.Demo_SceneGizmos_Summary,
            Assets: Assets.AssetSet.Empty,
            Create: context => new SceneGizmosDemo(context)),
        new(
            Id: "picking",
            Title: Strings.Keys.Demo_Picking_Title,
            Group: Strings.Keys.Group_Scene,
            Summary: Strings.Keys.Demo_Picking_Summary,
            Assets: Assets.AssetSet.Empty,
            Create: context => new PickingDemo(context)),
        new(
            Id: "post-processing",
            Title: Strings.Keys.Demo_PostProcessing_Title,
            Group: Strings.Keys.Group_Scene,
            Summary: Strings.Keys.Demo_PostProcessing_Summary,
            Assets: Assets.AssetSet.Empty,
            Create: context => new PostProcessingDemo(context)),
        new(
            Id: "gpu-lifecycle",
            Title: Strings.Keys.Demo_GpuLifecycle_Title,
            Group: Strings.Keys.Group_Scene,
            Summary: Strings.Keys.Demo_GpuLifecycle_Summary,
            Assets: Assets.AssetSet.Empty,
            Create: context => new GpuLifecycleDemo(context)),

        new(
            Id: "model-viewer",
            Title: Strings.Keys.Demo_ModelViewer_Title,
            Group: Strings.Keys.Group_Assets,
            Summary: Strings.Keys.Demo_ModelViewer_Summary,
            Assets: Assets.AssetManifest.RequireSet("Stool", "CoffeeTable", "LionHead"),
            Create: context => new ModelViewerDemo(context)),

        new(
            Id: "skinned-animation",
            Title: Strings.Keys.Demo_SkinnedAnimation_Title,
            Group: Strings.Keys.Group_Assets,
            Summary: Strings.Keys.Demo_SkinnedAnimation_Summary,
            Assets: Assets.AssetManifest.RequireSet("Soldier"),
            Create: context => new SkinnedAnimationDemo(context)),

        new(
            Id: "animation-mix",
            Title: Strings.Keys.Demo_AnimationMix_Title,
            Group: Strings.Keys.Group_Assets,
            Summary: Strings.Keys.Demo_AnimationMix_Summary,
            Assets: Assets.AssetManifest.RequireSet("Soldier"),
            Create: context => new AnimationMixDemo(context)),

        new(
            Id: "assimp-fbx",
            Title: Strings.Keys.Demo_AssimpFbx_Title,
            Group: Strings.Keys.Group_Assets,
            Summary: Strings.Keys.Demo_AssimpFbx_Summary,
            Assets: Assets.AssetManifest.RequireSet("FbxMannequin", "FbxIdle", "FbxJogFwd"),
            Create: context => new AssimpFbxDemo(context),
            // Assimp 要原生库，浏览器端没有。
            DesktopOnly: true),

        new(
            Id: "instancing",
            Title: Strings.Keys.Demo_Instancing_Title,
            Group: Strings.Keys.Group_Batching,
            Summary: Strings.Keys.Demo_Instancing_Summary,
            Assets: Assets.AssetSet.Empty,
            Create: context => new InstancingDemo(context),
            DefaultPipeline: PipelineKind.BlinnPhong,
            LockPipeline: true),
        new(
            Id: "hism",
            Title: Strings.Keys.Demo_Hism_Title,
            Group: Strings.Keys.Group_Batching,
            Summary: Strings.Keys.Demo_Hism_Summary,
            Assets: Assets.AssetSet.Empty,
            Create: context => new HismDemo(context)),
        new(
            Id: "particles",
            Title: Strings.Keys.Demo_Particles_Title,
            Group: Strings.Keys.Group_Batching,
            Summary: Strings.Keys.Demo_Particles_Summary,
            Assets: Assets.AssetManifest.RequireSet("ParticleFirePng"),
            Create: context => new ParticlesDemo(context)),
    ];

    /// <summary>
    /// 按标识或标题前缀查功能页，供深链使用。标题按当前语言比对，
    /// 所以稳定的一律是 <see cref="DemoDescriptor.Id"/>。
    /// </summary>
    /// <param name="idOrTitle"><see cref="DemoDescriptor.Id"/> 或标题片段。</param>
    /// <param name="web">是否浏览器端；为真时 <c>DesktopOnly</c> 的页面查不到。</param>
    public static DemoDescriptor? Resolve(string idOrTitle, bool web = false)
    {
        var candidates = All.Where(d => !web || !d.DesktopOnly).ToList();

        return candidates.FirstOrDefault(d => string.Equals(d.Id, idOrTitle, StringComparison.OrdinalIgnoreCase))
               ?? candidates.FirstOrDefault(d => d.Title.T().Contains(idOrTitle, StringComparison.OrdinalIgnoreCase));
    }
}
