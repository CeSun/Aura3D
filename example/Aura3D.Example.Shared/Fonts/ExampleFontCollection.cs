using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Fonts;

namespace Aura3D.Examples.Fonts;

/// <summary>
/// 内置的中文字体集合（Noto Sans SC 的 GB2312 子集，见 tools/fonts/generate-cjk-font.py）。
///
/// 浏览器宿主从操作系统取不到任何字体，不注册中文字体时界面上的中文一律是豆腐块；
/// 桌面与移动端也注册，这样四个平台的字形来源完全一致，示例在哪个平台上看到的排版就是哪个。
/// 拉丁与数字仍由 Inter 提供，中文靠 <see cref="ExampleFontAppBuilderExtensions"/> 登记的回退表出字。
/// </summary>
public sealed class ExampleFontCollection : EmbeddedFontCollection
{
    /// <inheritdoc />
    public ExampleFontCollection()
        : base(
            new Uri("fonts:Aura3D.Example.Shared", UriKind.Absolute),
            new Uri("avares://Aura3D.Example.Shared/Fonts", UriKind.Absolute))
    {
    }
}

/// <summary>
/// 字体注册用的 <see cref="AppBuilder"/> 扩展，与 <c>WithInterFont()</c> 并列，宿主各自在装配阶段调用一次。
///
/// 光注册集合还不够：字体管理器不会因为某个族已注册就自动拿它补字形，必须显式登记
/// <see cref="FontManagerOptions.FontFallbacks"/>。回退限定在东亚与符号区段（子集里除拉丁之外的部分），
/// 拉丁和数字仍由 Inter 出字，四个平台因此看到同一套排版。
/// </summary>
public static class ExampleFontAppBuilderExtensions
{
    /// <summary>
    /// 子集覆盖的非拉丁区段，取自 tools/fonts/generate-cjk-font.py 的字符表：
    /// 通用标点/箭头/数学/制表符号、CJK 符号与假名/注音/兼容区、汉字、竖排与全角形式。
    /// </summary>
    private static readonly UnicodeRange CjkRange = new(
    [
        new UnicodeRangeSegment(0x2000, 0x33FF),
        new UnicodeRangeSegment(0x4E00, 0x9FFF),
        new UnicodeRangeSegment(0xFE10, 0xFE4F),
        new UnicodeRangeSegment(0xFF00, 0xFFEF),
    ]);

    /// <summary>把 <see cref="ExampleFontCollection"/> 装进字体管理器，并登记为全局字形回退。</summary>
    public static AppBuilder WithAura3DExampleFonts(this AppBuilder appBuilder) =>
        appBuilder
            .With(new FontManagerOptions
            {
                FontFallbacks =
                [
                    new FontFallback
                    {
                        FontFamily = new FontFamily("fonts:Aura3D.Example.Shared#Noto Sans SC"),
                        UnicodeRange = CjkRange,
                    },
                ],
            })
            .ConfigureFonts(fontManager => fontManager.AddFontCollection(new ExampleFontCollection()));
}
