using Avalonia.Controls;

namespace Aura3D.Gallery;

/// <summary>
/// 桌面端主窗口。外壳由 <see cref="App"/> 造好后交进来，
/// 这样同一个外壳也能在浏览器与移动端直接当单视图用。
/// </summary>
public sealed partial class MainWindow : Window
{
    /// <summary>
    /// XAML 设计器与 <see cref="App"/> 用的无参构造。
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 装入外壳。
    /// </summary>
    /// <param name="shell">示例外壳。</param>
    public MainWindow(MainShell shell) : this() => ShellSlot.Content = shell;
}
