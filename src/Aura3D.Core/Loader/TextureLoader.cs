using Aura3D.Core.Resources;
using StbImageSharp;

namespace Aura3D.Core;

public static class TextureLoader
{
    public static Texture LoadTexture(Stream stream)
    {
        var texture = new Texture();


        ImageResult? imageResult = null;
        if (stream.CanSeek)
        {
            imageResult = ImageResult.FromStream(stream);
        }
        else
        {
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                ms.Seek(0, SeekOrigin.Begin);
                imageResult = ImageResult.FromStream(ms);
            }

        }

        texture.SetLdrData([..imageResult.Data], (uint)imageResult.Width, (uint)imageResult.Height)
            .SetColorFormat(imageResult.Comp switch
            {
                ColorComponents.RedGreenBlue => ColorFormat.RGB,
                ColorComponents.RedGreenBlueAlpha => ColorFormat.RGBA,
                _ => throw new NotSupportedException("Unsupported color format")
            })
            .SetMinFilter(TextureFilterMode.Linear)
            .SetMagFilter(TextureFilterMode.Linear);


        return texture;
    }


    public static Texture LoadTexture(byte[] bytes)
    { 
        using var stream = new MemoryStream(bytes);
        return LoadTexture(stream);
    }


    public static CubeTexture LoadCubeTexture(List<string> fileNames)
    {
        if (fileNames.Count != 6)
            throw new ArgumentException("Cube texture requires 6 images.");

        var streams = new List<Stream>();

        foreach (var fileName in fileNames)
        {
            streams.Add(new StreamReader(fileName).BaseStream);
        }

        try
        {
            return LoadCubeTexture(streams);
        }
        finally
        {
            foreach (var stream in streams)
            {
                stream.Dispose();
            }
        }

    }
    public static CubeTexture LoadCubeTexture(List<Stream> streams)
    {
        if (streams.Count != 6)
            throw new ArgumentException("Cube texture requires 6 images.");

        var cubeTexture = new CubeTexture();

        for (int i = 0; i < 6; i++)
        {
            ImageResult? imageResult = null;
            if (streams[i].CanSeek)
            {
                imageResult = ImageResult.FromStream(streams[i]);
            }
            else
            {
                using (var ms = new MemoryStream())
                {
                    streams[i].CopyTo(ms);
                    ms.Seek(0, SeekOrigin.Begin);
                    imageResult = ImageResult.FromStream(ms);
                }

            }

            cubeTexture.Data[i].AddRange(imageResult.Data);

            if (i == 0)
            {
                cubeTexture.Width = (uint)imageResult.Width;
                cubeTexture.Height = (uint)imageResult.Height;
                cubeTexture.IsGammaSpace = true;
                cubeTexture.ColorFormat = imageResult.Comp switch
                {
                    ColorComponents.RedGreenBlue => ColorFormat.RGB,
                    ColorComponents.RedGreenBlueAlpha => ColorFormat.RGBA,
                    _ => throw new NotSupportedException("Unsupported color format")
                };
            }
            else
            {
                if (cubeTexture.Width != imageResult.Width || cubeTexture.Height != imageResult.Height)
                    throw new ArgumentException("All images in a cube texture must have the same dimensions.");
                var colorFormat = imageResult.Comp switch
                {
                    ColorComponents.RedGreenBlue => ColorFormat.RGB,
                    ColorComponents.RedGreenBlueAlpha => ColorFormat.RGBA,
                    _ => throw new NotSupportedException("Unsupported color format")
                };
                if (cubeTexture.ColorFormat != colorFormat)
                    throw new ArgumentException("All images in a cube texture must have the same color format.");
            }

        }

        return cubeTexture;
    }


}
