using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;

namespace Example.ViewModels;

public partial class ContextLossViewModel : ViewModelBase
{
    private const int MaxLogEntries = 200;

    [ObservableProperty]
    private string _statusText = "等待 OpenGL 初始化…";

    [ObservableProperty]
    private string _statusColor = "#888888";

    [ObservableProperty]
    private int _lossCount;

    [ObservableProperty]
    private int _restoreCount;

    [ObservableProperty]
    private int _sceneBuildCount;

    [ObservableProperty]
    private int _frameCount;

    [ObservableProperty]
    private string _viewStateText = "视图未挂载";

    public ObservableCollection<string> Log { get; } = [];

    public void AddLog(string message)
    {
        Log.Insert(0, $"{DateTime.Now:HH:mm:ss.fff}  {message}");

        while (Log.Count > MaxLogEntries)
            Log.RemoveAt(Log.Count - 1);
    }
}
