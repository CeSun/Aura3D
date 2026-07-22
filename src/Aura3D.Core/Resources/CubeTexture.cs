using System.Numerics;
using System.Runtime.InteropServices;

namespace Aura3D.Core.Resources;

/// <summary>
/// Represents the cube texture type.
/// </summary>
public class CubeTexture : BaseTexture<CubeTexture>, IClone<CubeTexture>
{
    private List<byte>[] _data = [[], [], [], [], [], []];

    /// <summary>
    /// Performs the as ldr data operation.
    /// </summary>
    public ReadOnlySpan<byte> AsLdrData(int faceIndex)
    {
        ValidateFaceIndex(faceIndex);
        return IsHdr ? [] : CollectionsMarshal.AsSpan(_data[faceIndex]);
    }

    /// <summary>
    /// Performs the as hdr data operation.
    /// </summary>
    public ReadOnlySpan<float> AsHdrData(int faceIndex)
    {
        ValidateFaceIndex(faceIndex);
        return IsHdr ? MemoryMarshal.Cast<byte, float>(CollectionsMarshal.AsSpan(_data[faceIndex])) : [];
    }

    /// <summary>
    /// Sets the ldr face data.
    /// </summary>
    public CubeTexture SetLdrFaceData(int faceIndex, ReadOnlySpan<byte> data)
    {
        ValidateFaceIndex(faceIndex);
        _data[faceIndex] = new List<byte>(data.ToArray());
        IsHdr = false;
        MarkModified();
        return this;
    }

    /// <summary>
    /// Sets the hdr face data.
    /// </summary>
    public CubeTexture SetHdrFaceData(int faceIndex, ReadOnlySpan<float> data)
    {
        ValidateFaceIndex(faceIndex);
        _data[faceIndex] = ConvertHdrDataToBytes(data);
        IsHdr = true;
        MarkModified();
        return this;
    }

    /// <summary>
    /// Clones the associated data.
    /// </summary>
    public CubeTexture Clone()
    {
        var texture = new CubeTexture
        {
            Width = Width,
            Height = Height,
            IsHdr = IsHdr,
            WrapS = WrapS,
            WrapT = WrapT,
            WrapR = WrapR,
            MinFilter = MinFilter,
            MagFilter = MagFilter,
            ColorFormat = ColorFormat,
            IsGammaSpace = IsGammaSpace,
        };
        texture.SetFaceBuffers(_data);
        return texture;
    }

    /// <summary>
    /// Deep-clones the associated data.
    /// </summary>
    public CubeTexture DeepClone()
    {
        var texture = Clone();
        var data = new List<byte>[6];
        for (int i = 0; i < 6; i++)
        {
            data[i] = new List<byte>(_data[i]);
        }
        texture.SetFaceBuffers(data);
        return texture;
    }

    private TextureWrapMode _wrapR = TextureWrapMode.ClampToEdge;
    /// <summary>
    /// Gets the wrap r.
    /// </summary>
    public TextureWrapMode WrapR
    {
        get => _wrapR;
        set
        {
            if (_wrapR == value)
                return;
            _wrapR = value;
            MarkModified();
        }
    }

    internal void SetFaceBuffers(List<byte>[] data)
    {
        if (data.Length != 6)
            throw Aura3D.Core.Exceptions.ResourceErrors.CubeTextureFaceCount();

        _data = data;
    }

    private static void ValidateFaceIndex(int faceIndex)
    {
        if (faceIndex < 0 || faceIndex >= 6)
            throw new ArgumentOutOfRangeException(nameof(faceIndex));
    }

}


/// <summary>
/// Represents the hdri to cube texture converter type.
/// </summary>
public class HDRIToCubeTextureConverter
{

    /// <summary>
    /// Performs the convert from texture operation.
    /// </summary>
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
            var hdrFaces = new List<float>[6];
            for (int i = 0; i < 6; i++)
            {
                hdrFaces[i] = new List<float>((int)(cubeFaceSize * cubeFaceSize * channels));
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

                        hdrFaces[faceIndex].Add(rgba.X);
                        hdrFaces[faceIndex].Add(rgba.Y);
                        hdrFaces[faceIndex].Add(rgba.Z);
                        if (channels == 4)
                            hdrFaces[faceIndex].Add(rgba.W);
                    }
                }
            }

            for (int i = 0; i < 6; i++)
                cubeTexture.SetHdrFaceData(i, CollectionsMarshal.AsSpan(hdrFaces[i]));
        }
        else
        {
            var ldrFaces = new List<byte>[6];
            for (int i = 0; i < 6; i++)
            {
                ldrFaces[i] = new List<byte>((int)(cubeFaceSize * cubeFaceSize * channels));
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

                        ldrFaces[faceIndex].Add(ToLdrByte(rgba.X));
                        ldrFaces[faceIndex].Add(ToLdrByte(rgba.Y));
                        ldrFaces[faceIndex].Add(ToLdrByte(rgba.Z));
                        if (channels == 4)
                            ldrFaces[faceIndex].Add(ToLdrByte(rgba.W));
                    }
                }
            }

            for (int i = 0; i < 6; i++)
                cubeTexture.SetLdrFaceData(i, CollectionsMarshal.AsSpan(ldrFaces[i]));
        }
        return cubeTexture;

    }

    private static byte ToLdrByte(float value)
    {
        return (byte)global::System.Math.Clamp((int)MathF.Round(value * 255f), 0, 255);
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
        y = global::System.Math.Clamp(y, 0f, (float)height - 1f);

        int x0 = (int)MathF.Floor(x);
        int x1 = (x0 + 1) % (int)width;
        int y0 = (int)MathF.Floor(y);
        int y1 = System.Math.Min((int)MathF.Ceiling(y), (int)height - 1);

        float tx = x - x0;
        float ty = y - y0;
        bool hasAlpha = panorama.ColorFormat == ColorFormat.RGBA;

        Vector4 c00 = panorama.IsHdr ? GetPixel(panorama.AsHdrData(), width, x0, y0, hasAlpha) : GetPixel(panorama.AsLdrData(), width, x0, y0, hasAlpha);
        Vector4 c01 = panorama.IsHdr ? GetPixel(panorama.AsHdrData(), width, x0, y1, hasAlpha) : GetPixel(panorama.AsLdrData(), width, x0, y1, hasAlpha);
        Vector4 c10 = panorama.IsHdr ? GetPixel(panorama.AsHdrData(), width, x1, y0, hasAlpha) : GetPixel(panorama.AsLdrData(), width, x1, y0, hasAlpha);
        Vector4 c11 = panorama.IsHdr ? GetPixel(panorama.AsHdrData(), width, x1, y1, hasAlpha) : GetPixel(panorama.AsLdrData(), width, x1, y1, hasAlpha);

        Vector4 c0 = Vector4.Lerp(c00, c01, ty);
        Vector4 c1 = Vector4.Lerp(c10, c11, ty);
        Vector4 finalColor = Vector4.Lerp(c0, c1, tx);

        return finalColor;
    }

    private static Vector4 GetPixel(ReadOnlySpan<float> data, uint width, int x, int y, bool alpha)
    {
        int pixelIndex = (y * (int)width + x) * (alpha ? 4 : 3);

        if (pixelIndex + (alpha ? 3 : 2) >= data.Length)
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

    private static Vector4 GetPixel(ReadOnlySpan<byte> data, uint width, int x, int y, bool alpha)
    {
        int pixelIndex = (y * (int)width + x) * (alpha ? 4 : 3);

        if (pixelIndex + (alpha ? 3 : 2) >= data.Length)
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
