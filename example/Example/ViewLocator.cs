using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Example.Pages;
using Example.ViewModels;
namespace Example;

public class ViewLocator : IDataTemplate
{
    public Control? Build(object? data)
    {
        return data switch
        {
            BaseGeometriesViewModel baseGeometriesViewModel => new BaseGeometriesPage(),
            ModelPreviewViewModel gltfModelViewModel => new ModelPreviewPage(),
            RoboticArmViewModel roboticArmViewModel => new RoboticArmPage(),
            PbrViewModel pbrViewModel => new PbrPipelinePage(),
            CelShadingViewModel celShadingViewModel => new CelShadingPage(),
            CelShadingMaterialEditorViewModel celShadingMaterialEditorViewModel => new CelShadingMaterialEditorPage(),
            ParticleEditorViewModel particleEditorViewModel => new ParticleEditorPage(),
            PointCloudViewModel pointCloudViewModel => new PointCloudPage(),
            PrimitiveTypeViewModel primitiveTypeViewModel => new PrimitiveTypePage(),
            RenderingPerformanceViewModel renderingPerformanceViewModel => new RenderingPerformancePage(),
            AnimationFeaturesViewModel animationFeaturesViewModel => new AnimationFeaturesPage(),
            CascadedShadowMapsViewModel cascadedShadowMapsViewModel => new CascadedShadowMapsPage(),
            DebugTestViewModel debugTestViewModel => new DebugTestPage(),
            _ => new TextBlock() { Text = "NotFound" }
        };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
