using System.Globalization;
using Irihi.Lingua;

namespace Aura3D.Gallery.Localization;

/// <summary>
/// 从 C# 里取当前语言的文案。<see cref="LinguaKey"/> 的 <c>ToString()</c> 给的是键名，
/// 所以取值要经过管理器上的 observable；这里把这条链收成一个扩展方法，
/// 让登记表面可以写成 <c>Strings.Keys.Demo_Geometries_Title.T()</c>。
/// </summary>
public static class LinguaTextExtensions
{
    /// <summary>取该键在当前语言下的文本；没有对应译文时回落到键名，便于立刻看出漏翻。</summary>
    public static string T(this LinguaKey key)
    {
        // GetObservable 返回的实际对象带 CurrentValue，读它相当于读管理器的当前语言，
        // 不必为一次取名建订阅。类型不符时按未翻译处理，界面上会直接露出键名。
        var text = (key.Manager.GetObservable(key.Key) as LinguaObservable<string>)?.CurrentValue;

        return string.IsNullOrEmpty(text) ? key.Key : text;
    }

    /// <summary>把当前语言的文案当格式串用，例如「{0} 项 / {1} MB」。</summary>
    public static string Format(this LinguaKey key, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, key.T(), args);
}
