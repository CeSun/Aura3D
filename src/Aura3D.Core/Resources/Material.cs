using System.Drawing;

namespace Aura3D.Core.Resources;

public class Material : IClone<Material>
{
    public List<Channel> Channels { get; set; } = [];
    
    public BlendMode BlendMode { get; set; } = BlendMode.Opaque;

    public bool DoubleSided { get; set; } = false;

    public float AlphaCutoff { get; set; } = 0.5f;

    public Material Clone()
    {
        return new Material
        {
            BlendMode = this.BlendMode,
            DoubleSided = this.DoubleSided,
            AlphaCutoff = this.AlphaCutoff,
            Channels = Channels
        };
    }

    public Material DeepClone()
    {
        var material = Clone();

        material.Channels = new List<Channel>(Channels.Count);

        foreach(var channel in Channels)
        {
            var newChannel = new Channel
            {
                Name = channel.Name,
                Color = channel.Color,
                Texture = channel.Texture is Texture texture? texture.Clone() : null
            };
            material.Channels.Add(newChannel);
        }

        return material;
    }
}


public class Channel
{
    public string Name { get; set; } = string.Empty;

    public ITexture? Texture { get; set; }

    public Color Color { get; set; } = Color.White;
}

public enum BlendMode
{
    Opaque,
    Masked,
    Translucent,
}