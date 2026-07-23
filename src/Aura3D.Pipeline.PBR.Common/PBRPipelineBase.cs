using Aura3D.Core;
using Aura3D.Core.Renderers;
using Aura3D.Core.Resources;
using Aura3D.Core.Scenes;
using System.Drawing;

namespace Aura3D.Pipeline.PBR.Common;

public abstract class PBRPipelineBase : RenderPipeline
{
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
}
