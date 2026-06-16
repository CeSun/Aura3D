using Aura3D.Core.Resources;
using System.Drawing;
using Xunit;

namespace Aura3D.Tests.Resources;

public class TextureTests
{
    [Fact]
    public void CreateFromColor_ShouldCreate2x2LdrTexture()
    {
        var texture = Texture.CreateFromColor(Color.Red);

        Assert.Equal((uint)2, texture.Width);
        Assert.Equal((uint)2, texture.Height);
        Assert.False(texture.IsHdr);
        Assert.Equal(16, texture.LdrData.Count);
    }

    [Fact]
    public void DeepClone_ShouldCopyPixelBuffer()
    {
        var original = Texture.CreateFromColor(Color.Blue);
        var clone = original.DeepClone();

        Assert.NotSame(original, clone);
        Assert.NotSame(original.LdrData, clone.LdrData);
        Assert.Equal(original.LdrData, clone.LdrData);
    }
}
