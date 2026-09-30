using Irihi.Lingua;

namespace Aura3D.Gallery.Localization;

/// <summary>
/// 界面文案的唯一入口。源文件是 <c>Localization/Strings.resx</c>（英文，invariant）与
/// <c>Localization/Strings.zh-Hans.resx</c>（简体中文）；Irihi.Lingua 的源生成器在编译期把两份
/// 文案生成进本类：每个键一个 <see cref="Strings.Keys"/> 成员加一个 <c>IObservable&lt;string?&gt;</c> 属性，
/// 因此写错键名过不了编译，切换文化时已绑定的界面会自动收到新值。
///
/// 默认文化是英文（invariant），中文取 <c>zh-Hans</c>；见 <see cref="AppLanguage"/>。
/// </summary>
[LinguaManager("./Localization/Strings.resx")]
public partial class Strings;
