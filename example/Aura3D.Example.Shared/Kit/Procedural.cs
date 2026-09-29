using Aura3D.Core.Resources;
using System;

namespace Aura3D.Examples.Kit;

/// <summary>
/// 程序化生成的纹理。演示里的棋盘、柔光点、渐变天空等一律在这里现算，
/// 而不是附带图片文件——省下的是浏览器端真实的下载字节与 CPU 解码时间
/// （ wasm 单线程解一张 4K JPG 实测要 80 秒以上）。
/// </summary>
public static class Procedural
{
    /// <summary>
    /// 棋盘格，用于验证平铺、各向异性与 mipmap 表现。
    /// </summary>
    /// <param name="size">边长（像素）。</param>
    /// <param name="cells">每边格子数。</param>
    /// <param name="a">第一格颜色。</param>
    /// <param name="b">第二格颜色。</param>
    public static Texture Checker(int size = 256, int cells = 8, Rgb? a = null, Rgb? b = null)
    {
        return CheckerCore(size, cells, a ?? LightGrid, b ?? DarkGrid);
    }

    /// <summary>简单的 8 位 RGB 三元组，供程序化纹理的颜色参数用。</summary>
    public readonly record struct Rgb(byte R, byte G, byte B);

    /// <summary>棋盘亮色。</summary>
    public static readonly Rgb LightGrid = new(238, 238, 238);

    /// <summary>棋盘暗色。</summary>
    public static readonly Rgb DarkGrid = new(58, 58, 66);

    /// <summary>背景渐变顶色。</summary>
    public static readonly Rgb SkyTop = new(38, 60, 96);

    /// <summary>背景渐变底色。</summary>
    public static readonly Rgb SkyBottom = new(12, 14, 20);

    private static Texture CheckerCore(int size, int cells, Rgb a, Rgb b)
    {
        var data = new byte[size * size * 4];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var on = ((x * cells / size) + (y * cells / size)) % 2 == 0;

                var (r, g, bl) = on ? (a.R, a.G, a.B) : (b.R, b.G, b.B);

                Write(data, size, x, y, r, g, bl, 255);
            }
        }

        return FromRgba(data, size);
    }

    /// <summary>
    /// 径向渐变的柔光点，粒子系统的默认贴图形态。
    /// </summary>
    /// <param name="size">边长（像素）。</param>
    /// <param name="power">衰减指数，越大中心越锐利。</param>
    public static Texture SoftDot(int size = 128, float power = 2.2f)
    {
        var data = new byte[size * size * 4];

        float center = (size - 1) / 2f;
        float radius = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var dx = (x - center) / radius;
                var dy = (y - center) / radius;

                var d = MathF.Sqrt(dx * dx + dy * dy);

                var v = d >= 1f ? 0f : MathF.Pow(1f - d, power);

                var byteValue = (byte)Math.Round(v * 255f);

                Write(data, size, x, y, 255, 255, 255, byteValue);
            }
        }

        return FromRgba(data, size);
    }

    /// <summary>
    /// 细长火花，带中心高光的粒子贴图。
    /// </summary>
    public static Texture Spark(int width = 64, int height = 64)
    {
        var data = new byte[width * height * 4];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var u = x / (float)(width - 1);
                var v = y / (float)(height - 1);

                // 沿水平轴拉长的椭圆核，加一条更细的亮芯。
                var core = MathF.Exp(-MathF.Pow((v - 0.5f) * 6f, 2) - MathF.Pow((u - 0.5f) * 2.6f, 2));
                var filament = MathF.Exp(-MathF.Pow((v - 0.5f) * 16f, 2) - MathF.Pow((u - 0.5f) * 2f, 2));

                var a = Math.Clamp(core + filament * 0.8f, 0f, 1f);

                Write(data, width, x, y, 255, (byte)(200 + 55 * filament), (byte)(150 + 105 * filament), (byte)Math.Round(a * 255f));
            }
        }

        return FromRgba(data, width, height);
    }

    /// <summary>
    /// 竖向渐变的纯色背景，替代大尺寸背景图。
    /// </summary>
    /// <param name="top">顶部颜色。</param>
    /// <param name="bottom">底部颜色。</param>
    /// <param name="width">宽度，背景只做竖向插值故可以很小。</param>
    /// <param name="height">高度。</param>
    public static Texture VerticalGradient(Rgb? top = null, Rgb? bottom = null, int width = 8, int height = 256)
    {
        var from = top ?? SkyTop;
        var to = bottom ?? SkyBottom;

        var data = new byte[width * height * 4];

        for (int y = 0; y < height; y++)
        {
            var t = y / (float)(height - 1);

            var r = (byte)Math.Round(from.R + (to.R - from.R) * t);
            var g = (byte)Math.Round(from.G + (to.G - from.G) * t);
            var b = (byte)Math.Round(from.B + (to.B - from.B) * t);

            for (int x = 0; x < width; x++)
                Write(data, width, x, y, r, g, b, 255);
        }

        return FromRgba(data, width, height);
    }

    /// <summary>
    /// 切线空间法线贴图：把程序化高度场差分一次得到，用于材质通道演示而不带文件。
    /// </summary>
    /// <param name="size">边长（像素）。</param>
    /// <param name="bumps">波纹数量。</param>
    /// <param name="strength">法线偏转强度。</param>
    public static Texture BumpNormal(int size = 256, int bumps = 6, float strength = 1.6f)
    {
        float Height(float u, float v) =>
            0.5f + 0.5f * MathF.Sin(u * MathF.PI * 2f * bumps) * MathF.Cos(v * MathF.PI * 2f * bumps);

        var data = new byte[size * size * 4];

        float texel = 1f / size;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size;
                float v = y / (float)size;

                var dx = (Height(u + texel, v) - Height(u - texel, v)) * strength;
                var dy = (Height(u, v + texel) - Height(u, v - texel)) * strength;

                var length = MathF.Sqrt(dx * dx + dy * dy + 1f);

                Write(
                    data,
                    size,
                    x,
                    y,
                    (byte)Math.Round((-dx / length * 0.5f + 0.5f) * 255f),
                    (byte)Math.Round((-dy / length * 0.5f + 0.5f) * 255f),
                    (byte)Math.Round((1f / length * 0.5f + 0.5f) * 255f),
                    255);
            }
        }

        return FromRgba(data, size);
    }

    /// <summary>
    /// 金属度/粗糙度打包图（B=金属度、G=粗糙度，glTF 的 occlusion-roughness-metal 约定）。
    /// </summary>
    public static Texture MetalRough(int size = 128)
    {
        var data = new byte[size * size * 4];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var u = x / (float)size;
                var v = y / (float)size;

                var metal = u > 0.5f ? 255 : 0;
                var rough = (byte)(40 + (MathF.Sin(v * MathF.PI * 4f) * 0.5f + 0.5f) * 180f);

                Write(data, size, x, y, 255, rough, (byte)metal, 255);
            }
        }

        return FromRgba(data, size);
    }

    /// <summary>
    /// 纯色金属度/粗糙度图（B=金属度、G=粗糙度）。引擎没有 metallicFactor 这类数值因子，
    /// 想要一档纯金属或纯粗糙只有限这张图：4×4 足够，采样结果与边长无关。
    /// </summary>
    /// <param name="metal">金属度，0..1。</param>
    /// <param name="rough">粗糙度，0..1。</param>
    public static Texture MetalRoughSolid(float metal, float rough)
    {
        byte m = ToByte(metal);
        byte r = ToByte(rough);

        var data = new byte[4 * 4 * 4];

        for (int i = 0; i < 16; i++)
            Write(data, 4, i % 4, i / 4, 255, r, m, 255);

        // 通道图必须按原值取用，走 gamma 解码会把金属度改掉。
        return FromRgba(data, 4).SetColorFormat(ColorFormat.RGBA).SetIsGammaSpace(false);
    }

    /// <summary>
    /// 把 RGBA 字节数组包成一个 LDR 纹理，统一设好过滤与环绕。
    /// </summary>
    public static Texture FromRgba(byte[] rgba, int width, int? height = null)
    {
        var h = height ?? width;

        // 边长与字节数对不上时，上传的是越界读出来的垃圾，画面只会表现为「说不清为什么不对」。
        if (rgba.Length != width * h * 4)
            throw new ArgumentException($"texture declares {width}x{h} but got {rgba.Length} bytes (needs {width * h * 4}).");

        var texture = new Texture();

        texture.SetLdrData(rgba, (uint)width, (uint)h);
        texture.SetColorFormat(ColorFormat.RGBA);
        texture.SetIsGammaSpace(true);
        texture.MinFilter = TextureFilterMode.Linear;
        texture.MagFilter = TextureFilterMode.Linear;
        texture.WrapS = TextureWrapMode.Repeat;
        texture.WrapT = TextureWrapMode.Repeat;

        return texture;
    }

    private static byte ToByte(float value) => (byte)Math.Round(Math.Clamp(value, 0f, 1f) * 255f);

    private static void Write(byte[] data, int stride, int x, int y, byte r, byte g, byte b, byte a)
    {
        var i = (y * stride + x) * 4;

        data[i] = r;
        data[i + 1] = g;
        data[i + 2] = b;
        data[i + 3] = a;
    }
}
