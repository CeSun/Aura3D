using Silk.NET.OpenGLES;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Aura3D.Core.Resources;

/// <summary>
/// 立方体纹理类，用于天空盒等需要6面纹理的场景
/// </summary>
public class CubeTexture : BaseTexture<CubeTexture>, IGpuResource, ICubeTexture, IClone<CubeTexture>
{
    /// <summary>
    /// 是否需要上传到GPU
    /// </summary>
    public bool NeedsUpload { get; set; } = true;
    /// <summary>
    /// 纹理ID
    /// </summary>
    public uint TextureId { get; set; }

    public uint Width { get; set; }

    public uint Height { get; set; }

    public List<byte>[] LdrData { get; set; } = [[], [], [], [], [], []];
   public List<float>[] HdrData { get; set; } = [[], [], [], [], [], []];

    public unsafe void Upload(GL gl)
    {
        TextureId = gl.GenTexture();
        gl.BindTexture(GLEnum.TextureCubeMap, TextureId);

        for (int i = 0; i < 6; i++)
        {
            unsafe
            {
                if (IsHdr == false)
                {
                    fixed (void* p = CollectionsMarshal.AsSpan(LdrData[i]))
                    {
                        gl.TexImage2D(GLEnum.TextureCubeMapPositiveX + i, 0, GLInternalFormat, Width, Height, 0, GlFormat, GLEnum.UnsignedByte, p);
                    }
                }
                else
                {

                    fixed (void* p = CollectionsMarshal.AsSpan(HdrData[i]))
                    {
                        gl.TexImage2D(GLEnum.TextureCubeMapPositiveX + i, 0, GLInternalFormat, Width, Height, 0, GlFormat, GLEnum.Float, p);
                    }
                }

            }

        }

        gl.TexParameter(GLEnum.TextureCubeMap, TextureParameterName.TextureWrapR, (int)GlWarpR);

        gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)GlWarpS);

        gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)GlWarpT);

        gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)GlMagFilter);

        gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)GlMinFilter);

    }

    public void Destroy(GL gl)
    {
        if (TextureId != 0)
        {
            gl.DeleteTexture(TextureId);
            TextureId = 0;
        }
    }

    public CubeTexture Clone()
    {
        return new CubeTexture
        {
            TextureId = 0,
            Width = Width,
            Height = Height,
            LdrData = LdrData,
            HdrData = HdrData,
            IsHdr = IsHdr,
            WrapS = WrapS,
            WrapT = WrapT,
            WrapR = WrapR,
            MinFilter = MinFilter,
            MagFilter = MagFilter,
            ColorFormat = ColorFormat,
            IsGammaSpace = IsGammaSpace,
        };
    }

    public CubeTexture DeepClone()
    {
        var texture = Clone();
        texture.LdrData = new List<byte>[6];
        texture.HdrData = new List<float>[6];
        if (IsHdr == false)
        {
            for (int i = 0; i < 6; i++)
                texture.LdrData[i] = new List<byte>(LdrData[i]);
        }
        else
        {
            for (int i = 0; i < 6; i++)
                texture.HdrData[i] = new List<float>(HdrData[i]);
        }
        return texture;
    }

    public TextureWrapMode WrapR { get; set; } = TextureWrapMode.ClampToEdge;

    protected GLEnum GlWarpR => WrapR switch
    {
        TextureWrapMode.Repeat => GLEnum.Repeat,
        TextureWrapMode.MirroredRepeat => GLEnum.MirroredRepeat,
        TextureWrapMode.ClampToEdge => GLEnum.ClampToEdge,
        TextureWrapMode.ClampToBorder => GLEnum.ClampToBorder,
        _ => GLEnum.False
    };

}


/// <summary>
/// HDRI 到立方体纹理转换器
/// </summary>
public class HDRIToCubeTextureConverter
{

    /// <summary>
    /// 从全景纹理转换为立方体纹理
    /// </summary>
    /// <param name="texture">全景纹理</param>
    /// <param name="cubeFaceSize">立方体每个面的大小</param>
    /// <returns>立方体纹理</returns>
    public static CubeTexture ConvertFromTexture(Texture texture, uint cubeFaceSize)
    {
        var cubeTexture = new CubeTexture();

        cubeTexture.IsHdr = texture.IsHdr;

        cubeTexture.ColorFormat = texture.ColorFormat;

        cubeTexture.IsGammaSpace = texture.IsGammaSpace;

        cubeTexture.Width = cubeFaceSize;

        cubeTexture.Height = cubeFaceSize;

        int channels = texture.ColorFormat == ColorFormat.RGB ? 3 : 4;

        if (texture.IsHdr)
        {

            for (int i = 0; i < cubeTexture.HdrData.Length; i++)
            {
                cubeTexture.HdrData[i].Clear();
                cubeTexture.HdrData[i].Capacity = (int)(cubeFaceSize * cubeFaceSize * channels);
            }

        }
        else
        {
            for (int i = 0; i < cubeTexture.LdrData.Length; i++)
            {
                cubeTexture.LdrData[i].Clear();
                cubeTexture.LdrData[i].Capacity = (int)(cubeFaceSize * cubeFaceSize * channels);
            }
        }

        foreach (CubeFace face in Enum.GetValues(typeof(CubeFace)))
        {
            int faceIndex = (int)face;
            for (uint y = 0; y < cubeFaceSize; y++)
            {
                for (uint x = 0; x < cubeFaceSize; x++)
                {
                    Vector2 uv = new Vector2(
                        (float)x / cubeFaceSize * 2 - 1,
                        (float)y / cubeFaceSize * 2 - 1);

                    Vector3 direction = GetCubeFaceDirection(face, uv);
                    direction = Vector3.Normalize(direction);

                    Vector2 panoramaUV = DirectionToPanoramaUV(direction);

                    Vector4 rgba = SamplePanoramaTexture(texture, panoramaUV);

                    if (texture.IsHdr)
                    {
                        cubeTexture.HdrData[faceIndex].Add(rgba.X); // R
                        cubeTexture.HdrData[faceIndex].Add(rgba.Y); // G
                        cubeTexture.HdrData[faceIndex].Add(rgba.Z); // B
                        if (channels == 4)
                        {
                            cubeTexture.HdrData[faceIndex].Add(rgba.W); // A
                        }
                    }
                    else
                    {
                        cubeTexture.LdrData[faceIndex].Add((byte)(rgba.X * 255)); // R
                        cubeTexture.LdrData[faceIndex].Add((byte)(rgba.Y * 255)); // G
                        cubeTexture.LdrData[faceIndex].Add((byte)(rgba.Z * 255)); // B
                        if (channels == 4)
                        {
                            cubeTexture.LdrData[faceIndex].Add((byte)(rgba.W * 255)); // A
                        }

                    }
                }
            }
        }
        return cubeTexture;

    }

    private enum CubeFace
    {
        PositiveX = 0,  // 右 - 索引0
        NegativeX = 1,  // 左 - 索引1
        PositiveY = 2,  // 上 - 索引2
        NegativeY = 3,  // 下 - 索引3
        PositiveZ = 4,  // 前 - 索引4
        NegativeZ = 5   // 后 - 索引5
    }

    private static Vector3 GetCubeFaceDirection(CubeFace face, Vector2 uv)
    {
        return face switch
        {
            // 右（+X）：方向向量 = (1, -v, -u)
            CubeFace.PositiveX => new Vector3(1, -uv.Y, -uv.X),
            // 左（-X）：方向向量 = (-1, -v, u)
            CubeFace.NegativeX => new Vector3(-1, -uv.Y, uv.X),
            // 上（+Y）：方向向量 = (u, 1, v)
            CubeFace.PositiveY => new Vector3(uv.X, 1, uv.Y),
            // 下（-Y）：方向向量 = (u, -1, -v)
            CubeFace.NegativeY => new Vector3(uv.X, -1, -uv.Y),
            // 前（+Z）：方向向量 = (u, -v, 1)
            CubeFace.PositiveZ => new Vector3(uv.X, -uv.Y, 1),
            // 后（-Z）：方向向量 = (-u, -v, -1)
            CubeFace.NegativeZ => new Vector3(-uv.X, -uv.Y, -1),
            _ => Vector3.Zero
        };
    }

    private static Vector2 DirectionToPanoramaUV(Vector3 dir)
    {
        dir = Vector3.Normalize(dir);

        float longitude = (float)MathF.Atan2(dir.Z, dir.X);
        float clampedY = dir.Y < -1f ? -1f : (dir.Y > 1f ? 1f : dir.Y);
        float latitude = (float)MathF.Acos(clampedY);


        float u = (longitude / (2 * MathF.PI)) + 0.5f;
        float v = latitude / MathF.PI;

        return new Vector2(u, v);
    }

    private static Vector4 SamplePanoramaTexture(Texture panorama, Vector2 uv)
    {
        uint width = panorama.Width;
        uint height = panorama.Height;

        float x = uv.X * width;
        float y = uv.Y * height;

        x = x % width;
        if (x < 0) x += width;

        int x0 = (int)MathF.Floor(x);
        int x1 = (x0 + 1) % (int)width;
        int y0 = (int)MathF.Floor(y);
        int y1 = System.Math.Min((int)MathF.Ceiling(y), (int)height - 1);

        float tx = x - x0;
        float ty = y - y0;
        bool hasAlpha = panorama.ColorFormat == ColorFormat.RGBA;

        Vector4 c00 = panorama.IsHdr ? GetPixel(panorama.HdrData, width, x0, y0, hasAlpha) : GetPixel(panorama.LdrData, width, x0, y0, hasAlpha);
        Vector4 c01 = panorama.IsHdr ? GetPixel(panorama.HdrData, width, x0, y1, hasAlpha) : GetPixel(panorama.LdrData, width, x0, y1, hasAlpha);
        Vector4 c10 = panorama.IsHdr ? GetPixel(panorama.HdrData, width, x1, y0, hasAlpha) : GetPixel(panorama.LdrData, width, x1, y0, hasAlpha);
        Vector4 c11 = panorama.IsHdr ? GetPixel(panorama.HdrData, width, x1, y1, hasAlpha) : GetPixel(panorama.LdrData, width, x1, y1, hasAlpha);

        Vector4 c0 = Vector4.Lerp(c00, c01, ty);
        Vector4 c1 = Vector4.Lerp(c10, c11, ty);
        Vector4 finalColor = Vector4.Lerp(c0, c1, tx);

        return finalColor;
    }

    private static Vector4 GetPixel(List<float> data, uint width, int x, int y, bool alpha)
    {
        int pixelIndex = (y * (int)width + x) * (alpha ? 4 : 3);

        if (pixelIndex + (alpha ? 3 : 2) >= data.Count)
            return Vector4.Zero;

        float r = data[pixelIndex];
        float g = data[pixelIndex + 1];
        float b = data[pixelIndex + 2];
        float a = 0;
        if (alpha)
        {
            a = data[pixelIndex + 3];
        }

        return new Vector4(r, g, b, a);
    }

    private static Vector4 GetPixel(List<byte> data, uint width, int x, int y, bool alpha)
    {
        int pixelIndex = (y * (int)width + x) * (alpha ? 4 : 3);

        if (pixelIndex + (alpha ? 3 : 2) >= data.Count)
            return Vector4.Zero;

        float r = data[pixelIndex] / (float)255;
        float g = data[pixelIndex + 1] / (float)255;
        float b = data[pixelIndex + 2] / (float)255;
        float a = 0;
        if (alpha)
        {
            a = data[pixelIndex + 3] / (float)255;
        }


        return new Vector4(r, g, b, a);
    }

}