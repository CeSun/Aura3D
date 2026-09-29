using Aura3D.Examples.Assets;
using Aura3D.Examples.Localization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Aura3D.Examples;

/// <summary>
/// 目录里的一个功能页条目。外壳的 DataTemplate 绑这些字段，点击时靠 <see cref="Id"/> 回查登记表。
/// </summary>
public sealed class NavEntry(DemoDescriptor descriptor) : INotifyPropertyChanged
{
    private bool isSelected;

    /// <summary>深链标识。</summary>
    public string Id => descriptor.Id;

    /// <summary>标题，按当前语言取。</summary>
    public string Title => descriptor.Title.T();

    /// <summary>体积说明，让浏览器的使用者在点开重页前知道要下载多少。</summary>
    public string SizeLabel => descriptor.SizeLabel;

    /// <summary>是否当前页。模板用它切换高亮样式。</summary>
    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (isSelected == value)
                return;

            isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// 目录的一个分组：分组名 + 组内条目。
/// </summary>
public sealed class NavGroup(string title, IReadOnlyList<NavEntry> entries)
{
    /// <summary>分组名。</summary>
    public string Title { get; } = title;

    /// <summary>组内条目。</summary>
    public IReadOnlyList<NavEntry> Entries { get; } = entries;
}

/// <summary>
/// 示例应用的主外壳：左侧功能目录、中间功能页宿主。
/// 目录完全由 <see cref="DemoRegistry"/> 生成，因此新增功能页不需要动这里；
/// 窄屏（手机、浏览器小窗）下目录自动收起，把宽度让给视口。
/// </summary>
public sealed partial class MainShell : UserControl
{
    private const double NarrowWidth = 860;

    /// <summary>依赖的属性：目录是否展开。</summary>
    public static readonly StyledProperty<bool> NavPinnedProperty =
        AvaloniaProperty.Register<MainShell, bool>(nameof(NavPinned), true);

    /// <summary>依赖的属性：页头标题。</summary>
    public static readonly StyledProperty<string> PageTitleProperty =
        AvaloniaProperty.Register<MainShell, string>(nameof(PageTitle));

    /// <summary>依赖的属性：页头说明。</summary>
    public static readonly StyledProperty<string> PageSummaryProperty =
        AvaloniaProperty.Register<MainShell, string>(nameof(PageSummary));

    /// <summary>依赖的属性：目录搜索词。</summary>
    public static readonly StyledProperty<string> KeywordProperty =
        AvaloniaProperty.Register<MainShell, string>(nameof(Keyword), string.Empty);

    /// <summary>依赖的属性：目录内容（按分组排好）。</summary>
    public static readonly StyledProperty<IReadOnlyList<NavGroup>> GroupsProperty =
        AvaloniaProperty.Register<MainShell, IReadOnlyList<NavGroup>>(nameof(Groups));

    static MainShell()
    {
        // 搜索词一变就重建目录视图：条目只有二十几个，过滤比重用控件便宜。
        KeywordProperty.Changed.AddClassHandler<MainShell>((shell, _) => shell.RebuildGroups());

        // 外壳自身宽度变化用来做窄屏收起，不需要等布局回调。
        BoundsProperty.Changed.AddClassHandler<MainShell>((shell, _) => shell.UpdateNavForWidth());
    }

    private readonly string? startup;
    private string? selectedId;
    private bool wasNarrow;

    /// <summary>
    /// 构建外壳并选定首屏功能页。
    /// </summary>
    /// <param name="assets">资产提供器。</param>
    /// <param name="startup">启动时要直接进入的功能页标识（深链），为空则取目录第一项。</param>
    public MainShell(IAssetProvider assets, string? startup)
    {
        this.startup = startup;

        InitializeComponent();

        // 外壳的 XAML 绑的是下面这些自己的属性；功能页宿主在构造时显式设了自己的
        // DataContext，所以这里不会污染页面绑定。
        DataContext = this;

        Host.Assets = assets;

        RebuildGroups();

        // 文案表是全局的，切语言时目录、页头和当前页都要按新语言重出一遍。
        AppLanguage.Changed += OnLanguageChanged;

        Loaded += (_, _) => Select(startup ?? DemoRegistry.All.First().Id);
    }

    /// <summary>当前功能页宿主。</summary>
    public DemoHost PageHost => Host;

    /// <summary>目录是否展开。</summary>
    public bool NavPinned
    {
        get => GetValue(NavPinnedProperty);
        set => SetValue(NavPinnedProperty, value);
    }

    /// <summary>页头标题。</summary>
    public string PageTitle
    {
        get => GetValue(PageTitleProperty);
        set => SetValue(PageTitleProperty, value);
    }

    /// <summary>页头说明。</summary>
    public string PageSummary
    {
        get => GetValue(PageSummaryProperty);
        set => SetValue(PageSummaryProperty, value);
    }

    /// <summary>目录搜索词。</summary>
    public string Keyword
    {
        get => GetValue(KeywordProperty);
        set => SetValue(KeywordProperty, value);
    }

    /// <summary>目录内容。</summary>
    public IReadOnlyList<NavGroup> Groups
    {
        get => GetValue(GroupsProperty);
        set => SetValue(GroupsProperty, value);
    }

    /// <summary>
    /// 当前显示的功能页标识，深链同步与验证脚本读取用。
    /// </summary>
    public string? CurrentId => Host.CurrentId;

    /// <summary>当前页的场景是否已组装完成。</summary>
    public bool SceneBuilt => Host.SceneBuilt;

    /// <summary>
    /// 按标识切页，供 URL 变化或外部驱动调用。
    /// </summary>
    /// <param name="id">功能页标识，接受完整 Id 或标题前缀。</param>
    public void Select(string id)
    {
        var target = DemoRegistry.Resolve(id);

        if (target == null)
            return;

        selectedId = target.Id;

        if (string.IsNullOrEmpty(Keyword) == false)
            Keyword = string.Empty;

        PageTitle = target.Title.T();
        PageSummary = target.Summary.T();

        foreach (var group in Groups)
            foreach (var entry in group.Entries)
                entry.IsSelected = entry.Id == target.Id;

        // 窄屏下选中即收起目录，让视口拿到整幅宽度。
        if (Bounds.Width < NarrowWidth)
            NavPinned = false;

        _ = Host.LoadAsync(target);
    }

    private void OnNavClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string id })
            Select(id);
    }

    private void OnLanguageClicked(object? sender, RoutedEventArgs e) => AppLanguage.Toggle();

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        RebuildGroups();

        // 页内文案是构造时取值的（行标签、说明段），只能整页重挂一次才能跟着换语言。
        if (selectedId is { } id)
            Select(id);
    }

    private void RebuildGroups()
    {
        var keyword = Keyword ?? string.Empty;

        var matches = DemoRegistry.All.Where(d => Matches(d, keyword)).ToList();

        Groups = matches
            .GroupBy(d => d.Group)
            .Select(g => new NavGroup(g.Key.T(), g.Select(d => new NavEntry(d)
            {
                IsSelected = d.Id == selectedId,
            }).ToList()))
            .ToList();
    }

    private static bool Matches(DemoDescriptor descriptor, string keyword) =>
        string.IsNullOrWhiteSpace(keyword)
        || descriptor.Title.T().Contains(keyword, StringComparison.OrdinalIgnoreCase)
        || descriptor.Summary.T().Contains(keyword, StringComparison.OrdinalIgnoreCase)
        || descriptor.Group.T().Contains(keyword, StringComparison.OrdinalIgnoreCase);

    private void UpdateNavForWidth()
    {
        var narrow = Bounds.Width < NarrowWidth && Bounds.Width > 0;

        if (narrow && wasNarrow == false)
            NavPinned = false;

        wasNarrow = narrow;
    }
}
