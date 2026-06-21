using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpGLTF.Schema2;
using System.Numerics;
using JSONREADER = System.Text.Json.Utf8JsonReader;
using JSONWRITER = System.Text.Json.Utf8JsonWriter;
using System.Runtime.CompilerServices;
using Aura3D.Model;
using Aura3D.Core;
using Aura3D.Core.Renderers;

namespace Aura3D.Pipeline.CelShading;

public class CelMaterialExtensionLoader : MaterialExtensionLoaderBase
{
    public override string Name => "AURA3D_TEXTURES_CELSHADING";

    [ModuleInitializer]
    internal static void Init()
    {
        // Register Extension
        ModelLoader.RegisterMaterialExtension<Aura3DCelExtraProperties>(
            () => new CelMaterialExtensionLoader()
            );
    }

    private static Core.Resources.Texture? GetTextureAtIndex(ModelRoot modelRoot, int index)
    {
        if (index < 0 || index > modelRoot.LogicalTextures.Count)
            return null;
        SharpGLTF.Schema2.Texture glTexture = modelRoot.LogicalTextures[index];

        var data = glTexture.PrimaryImage.Content.Content;
        var tex = TextureLoader.LoadTexture(data.ToArray());
        return tex;
    }

    public override void LoadMaterialExtension(ModelRoot modelRoot, SharpGLTF.Schema2.Material modelMaterial, Core.Resources.Material logicMaterial)
    {
        // Read Texture
        foreach (var extension in modelMaterial.Extensions)
        {
            if (!(extension.GetType() == typeof(Aura3DCelExtraProperties)))
                continue;

            Aura3DCelExtraProperties celExt = (Aura3DCelExtraProperties)extension;
            // var ILMTexture = GetTextureAtIndex(modelRoot, celExt.ILM);

            string[] texturesNames = { "ILM", "SDF", "ShadowRamp", "SpecularRamp" };
            int i = 0;
            foreach (int textureIdx in new int[] { celExt.ILM, celExt.SDF, celExt.ShadowRamp, celExt.SpecularRamp })
            {
                var texture = GetTextureAtIndex(modelRoot, textureIdx);
                if (texture == null)
                {
                    ++i;
                    continue;
                }
                    logicMaterial.SetChannel(new Core.Resources.Channel
                    {
                        Name = texturesNames[i],
                        Texture = texture
                    });
                    ++i;
                }
            logicMaterial.SetParameterValue<int>("RenderType", celExt.RenderType);

            logicMaterial.SetParameterValue<float>("_RampIndex0", celExt.RampIndex0);
            logicMaterial.SetParameterValue<float>("_RampIndex1", celExt.RampIndex1);
            logicMaterial.SetParameterValue<float>("_RampIndex2", celExt.RampIndex2);
            logicMaterial.SetParameterValue<float>("_RampIndex3", celExt.RampIndex3);
            logicMaterial.SetParameterValue<float>("_RampIndex4", celExt.RampIndex4);

            logicMaterial.SetParameterValue<float>("_BrightFac", celExt.BrightFac);
            logicMaterial.SetParameterValue<float>("_GreyFac", celExt.GreyFac);
            logicMaterial.SetParameterValue<float>("_DarkFac", celExt.DarkFac);
            logicMaterial.SetParameterValue<float>("_BrightAreaShadowFac", celExt.BrightAreaShadowFac);

            logicMaterial.SetParameterValue<Vector4>("_LightAreaColorTint", celExt.LightAreaColorTint);
            logicMaterial.SetParameterValue<Vector4>("_DarkShadowColor", celExt.DarkShadowColor);
            logicMaterial.SetParameterValue<Vector4>("_CoolDarkShadowColor", celExt.CoolDarkShadowColor);

            logicMaterial.SetParameterValue<float>("_FaceShadowOffset", celExt.FaceShadowOffset);
            logicMaterial.SetParameterValue<float>("_FaceShadowTransitionSoftness", celExt.FaceShadowTransitionSoftness);

            break;
        }
    }

}

public class Aura3DCelExtraProperties : ExtraProperties
{
    public string Name => SCHEMANAME;

    public new const string SCHEMANAME = "AURA3D_TEXTURES_CELSHADING";
    protected override string GetSchemaName() => SCHEMANAME;

    public Aura3DCelExtraProperties() { }

    //private static readonly List<string> proppertyNames = new List<string>{ "ILM", "SDF", "ShadowRamp", "SpecularRamp",
    //    "_RampIndex0", "_RampIndex1", "_RampIndex2", "_RampIndex3", "_RampIndex4",
    //    "_BrightFac", "_GreyFac", "_DarkFac", "_BrightAreaShadowFac", 
    //    "_BrightAreaShadowFac", "_DarkShadowColor", "_CoolDarkShadowColor",
    //    "_FaceShadowOffset", "_FaceShadowTransitionSoftness"
    //};

    [ModuleInitializer]
    internal static void Init()
    {
        // Register Extension
        SharpGLTF.Schema2.ExtensionsFactory.RegisterExtension<SharpGLTF.Schema2.Material, Aura3DCelExtraProperties>(SCHEMANAME, _ => new Aura3DCelExtraProperties());
    }

    #region data

    /// <summary>
    /// ILM 纹理索引。
    /// </summary>
    public int ILM;

    /// <summary>
    /// SDF 纹理索引。
    /// </summary>
    public int SDF;

    /// <summary>
    /// 阴影渐变纹理索引。
    /// </summary>
    public int ShadowRamp;

    /// <summary>
    /// 高光渐变纹理索引。
    /// </summary>
    public int SpecularRamp;

    // 渲染类型，0为body，1为face
    public int RenderType;

    // 材质绑定的骨骼
    public string BoneName;

    // Ramp Index：
    public float RampIndex0;
    public float RampIndex1;
    public float RampIndex2;
    public float RampIndex3;
    public float RampIndex4;

    // Light Factor
    public float BrightFac;
    public float GreyFac;
    public float DarkFac;
    public float BrightAreaShadowFac;

    // Color Tint
    public Vector4 LightAreaColorTint;
    public Vector4 DarkShadowColor;
    public Vector4 CoolDarkShadowColor;

    // SDF Offset
    public float FaceShadowOffset;
    public float FaceShadowTransitionSoftness;

    #endregion

    private static Core.Resources.Texture? GetTextureAtIndex(ModelRoot modelRoot, int index)
    {
        if (index < 0 || index > modelRoot.LogicalTextures.Count)
            return null;
        SharpGLTF.Schema2.Texture glTexture = modelRoot.LogicalTextures[index];

        var data = glTexture.PrimaryImage.Content.Content;
        var tex = TextureLoader.LoadTexture(data.ToArray());
        return tex;
    }

    public static List<Core.Resources.Channel> GetExtenionChannels(ModelRoot modelRoot, SharpGLTF.Schema2.Material material)
    {
        List<Core.Resources.Channel> channels = new List<Core.Resources.Channel>();
        foreach (var extension in material.Extensions)
        {
            if (extension.GetType() == typeof(Aura3DCelExtraProperties))
            {
                Aura3DCelExtraProperties celExt = (Aura3DCelExtraProperties)extension;
                var ILMTexture = GetTextureAtIndex(modelRoot, celExt.ILM);

                string[] texturesNames = { "ILM", "SDF", "ShadowRamp", "SpecularRamp" };
                int i = 0;
                foreach (int textureIdx in new int[] { celExt.ILM, celExt.SDF, celExt.ShadowRamp, celExt.SpecularRamp })
                {
                    var texture = GetTextureAtIndex(modelRoot, textureIdx);
                    if (texture == null)
                    {
                        ++i;
                        continue;
                    }
                    channels.Add(new Core.Resources.Channel
                    {
                        Name = texturesNames[i],
                        Texture = texture
                    });
                    ++i;
                }
            }
        }

        return channels;
    }

    #region serialization

    protected override void SerializeProperties(JSONWRITER writer)
    {
        base.SerializeProperties(writer);

        SerializeProperty(writer, "RenderType", RenderType);
        SerializeProperty(writer, "BoneName", BoneName);

        SerializeProperty(writer, "ILM", ILM);
        SerializeProperty(writer, "SDF", SDF);
        SerializeProperty(writer, "ShadowRamp", ShadowRamp);
        SerializeProperty(writer, "SpecularRamp", SpecularRamp);

        SerializeProperty(writer, "_RampIndex0", RampIndex0);
        SerializeProperty(writer, "_RampIndex1", RampIndex1);
        SerializeProperty(writer, "_RampIndex2", RampIndex2);
        SerializeProperty(writer, "_RampIndex3", RampIndex3);
        SerializeProperty(writer, "_RampIndex4", RampIndex4);

        SerializeProperty(writer, "_BrightFac", BrightFac);
        SerializeProperty(writer, "_GreyFac", GreyFac);
        SerializeProperty(writer, "_DarkFac", DarkFac);
        SerializeProperty(writer, "_BrightAreaShadowFac", BrightAreaShadowFac);

        SerializeProperty(writer, "_LightAreaColorTint", LightAreaColorTint);
        SerializeProperty(writer, "_DarkShadowColor", DarkShadowColor);
        SerializeProperty(writer, "_CoolDarkShadowColor", CoolDarkShadowColor);

        SerializeProperty(writer, "_FaceShadowOffset", FaceShadowOffset);
        SerializeProperty(writer, "_FaceShadowTransitionSoftness", FaceShadowTransitionSoftness);
    }

    protected override void DeserializeProperty(string jsonPropertyName, ref JSONREADER reader)
    {
        switch (jsonPropertyName)
        {
            case "RenderType": DeserializePropertyValue<Aura3DCelExtraProperties, int>(ref reader, this, out RenderType); break;
            case "BoneName": DeserializePropertyValue<Aura3DCelExtraProperties, string>(ref reader, this, out BoneName); break;

            case "ILM": DeserializePropertyValue<Aura3DCelExtraProperties, int>(ref reader, this, out ILM); break;
            case "SDF": DeserializePropertyValue<Aura3DCelExtraProperties, int>(ref reader, this, out SDF); break;
            case "ShadowRamp": DeserializePropertyValue<Aura3DCelExtraProperties, int>(ref reader, this, out ShadowRamp); break;
            case "SpecularRamp": DeserializePropertyValue<Aura3DCelExtraProperties, int>(ref reader, this, out SpecularRamp); break;

            case "_RampIndex0": DeserializePropertyValue<Aura3DCelExtraProperties, float>(ref reader, this, out RampIndex0); break;
            case "_RampIndex1": DeserializePropertyValue<Aura3DCelExtraProperties, float>(ref reader, this, out RampIndex1); break;
            case "_RampIndex2": DeserializePropertyValue<Aura3DCelExtraProperties, float>(ref reader, this, out RampIndex2); break;
            case "_RampIndex3": DeserializePropertyValue<Aura3DCelExtraProperties, float>(ref reader, this, out RampIndex3); break;
            case "_RampIndex4": DeserializePropertyValue<Aura3DCelExtraProperties, float>(ref reader, this, out RampIndex4); break;

            case "_BrightFac": DeserializePropertyValue<Aura3DCelExtraProperties, float>(ref reader, this, out BrightFac); break;
            case "_GreyFac": DeserializePropertyValue<Aura3DCelExtraProperties, float>(ref reader, this, out GreyFac); break;
            case "_DarkFac": DeserializePropertyValue<Aura3DCelExtraProperties, float>(ref reader, this, out DarkFac); break;
            case "_BrightAreaShadowFac": DeserializePropertyValue<Aura3DCelExtraProperties, float>(ref reader, this, out BrightAreaShadowFac); break;

            case "_LightAreaColorTint": DeserializePropertyValue<Aura3DCelExtraProperties, Vector4>(ref reader, this, out LightAreaColorTint); break;
            case "_DarkShadowColor": DeserializePropertyValue<Aura3DCelExtraProperties, Vector4>(ref reader, this, out DarkShadowColor); break;
            case "_CoolDarkShadowColor": DeserializePropertyValue<Aura3DCelExtraProperties, Vector4>(ref reader, this, out CoolDarkShadowColor); break;

            case "_FaceShadowOffset": DeserializePropertyValue<Aura3DCelExtraProperties, float>(ref reader, this, out FaceShadowOffset); break;
            case "_FaceShadowTransitionSoftness": DeserializePropertyValue<Aura3DCelExtraProperties, float>(ref reader, this, out FaceShadowTransitionSoftness); break;

            default: base.DeserializeProperty(jsonPropertyName, ref reader); break;
        }
    }

    #endregion
}
