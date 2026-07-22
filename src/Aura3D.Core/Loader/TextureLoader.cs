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
                _ => throw Aura3D.Core.Exceptions.TextureLoaderErrors.UnsupportedColorFormat()
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
            throw Aura3D.Core.Exceptions.TextureLoaderErrors.CubeTextureImageCount();

        var streams = new List<Stream>();

        foreach (var fileName in fileNames)
        {
            streams.Add(File.OpenRead(fileName));
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
            throw Aura3D.Core.Exceptions.TextureLoaderErrors.CubeTextureImageCount();

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

            cubeTexture.SetLdrFaceData(i, imageResult.Data);

            if (i == 0)
            {
                cubeTexture.Width = (uint)imageResult.Width;
                cubeTexture.Height = (uint)imageResult.Height;
                cubeTexture.IsGammaSpace = true;
                cubeTexture.ColorFormat = imageResult.Comp switch
                {
                    ColorComponents.RedGreenBlue => ColorFormat.RGB,
                    ColorComponents.RedGreenBlueAlpha => ColorFormat.RGBA,
                    _ => throw Aura3D.Core.Exceptions.TextureLoaderErrors.UnsupportedColorFormat()
                };
            }
            else
            {
                if (cubeTexture.Width != imageResult.Width || cubeTexture.Height != imageResult.Height)
                    throw Aura3D.Core.Exceptions.TextureLoaderErrors.CubeTextureDimensionMismatch();
                var colorFormat = imageResult.Comp switch
                {
                    ColorComponents.RedGreenBlue => ColorFormat.RGB,
                    ColorComponents.RedGreenBlueAlpha => ColorFormat.RGBA,
                    _ => throw Aura3D.Core.Exceptions.TextureLoaderErrors.UnsupportedColorFormat()
                };
                if (cubeTexture.ColorFormat != colorFormat)
                    throw Aura3D.Core.Exceptions.TextureLoaderErrors.CubeTextureColorFormatMismatch();
            }

        }

        return cubeTexture;
    }

    public static Texture LoadHdrTexture(Stream stream)
    {
        var texture = new Texture();

        ImageResultFloat? imageResult = null;
        if (stream.CanSeek)
        {
            imageResult = ImageResultFloat.FromStream(stream);
        }
        else
        {
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                ms.Seek(0, SeekOrigin.Begin);
                imageResult = ImageResultFloat.FromStream(ms);
            }

        }

        texture.SetHdrData(imageResult.Data, (uint)imageResult.Width, (uint)imageResult.Height)
            .SetColorFormat(imageResult.Comp switch
            {
                ColorComponents.RedGreenBlue => ColorFormat.RGB,
                ColorComponents.RedGreenBlueAlpha => ColorFormat.RGBA,
                _ => throw Aura3D.Core.Exceptions.TextureLoaderErrors.UnsupportedColorFormat()
            })
            .SetMinFilter(TextureFilterMode.Linear)
            .SetMagFilter(TextureFilterMode.Linear);


        return texture;
    }
}
