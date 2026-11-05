using System.Windows;
using System.Windows.Controls;

namespace MiniEnvi.UI;

public sealed class MainMenuWindow : Window
{
    public MainMenuWindow()
    {
        Title = "MiniENVI Classic";
        Width = 940;
        Height = 78;
        MinWidth = 700;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = 25;
        Top = 20;

        var menu = new Menu { FontSize = 15, VerticalContentAlignment = VerticalAlignment.Center };
        var file = new MenuItem { Header = "文件(_F)" };
        Add(file, "打开 ENVI 数据...", () => OpenRequested?.Invoke(this, EventArgs.Empty));
        Add(file, "关闭数据", () => CloseDatasetRequested?.Invoke(this, EventArgs.Empty));
        file.Items.Add(new Separator());
        Add(file, "退出", Close);

        var basic = new MenuItem { Header = "基本工具(_B)" };
        Add(basic, "元信息...", () => MetadataRequested?.Invoke(this, EventArgs.Empty));
        Add(basic, "统计信息...", () => StatisticsRequested?.Invoke(this, EventArgs.Empty));
        Add(basic, "矩形裁剪...", () => CropRequested?.Invoke(this, EventArgs.Empty));

        var window = new MenuItem { Header = "窗口(_W)" };
        Add(window, "恢复所有窗口", () => ShowWindowsRequested?.Invoke(this, EventArgs.Empty));
        Add(window, "排列三级显示窗口", () => ArrangeRequested?.Invoke(this, EventArgs.Empty));

        var help = new MenuItem { Header = "帮助(_H)" };
        Add(help, "关于 MiniENVI", () => MessageBox.Show(this,
            "MiniENVI Classic\n遥感原理与应用课程作业",
            "关于", MessageBoxButton.OK, MessageBoxImage.Information));

        menu.Items.Add(file);
        menu.Items.Add(basic);
        foreach (var name in new[] { "分类", "变换", "滤波", "光谱", "地图", "矢量" })
            menu.Items.Add(new MenuItem { Header = name, IsEnabled = false });
        menu.Items.Add(window);
        menu.Items.Add(help);
        Content = menu;
    }

    public event EventHandler? OpenRequested;
    public event EventHandler? CloseDatasetRequested;
    public event EventHandler? MetadataRequested;
    public event EventHandler? StatisticsRequested;
    public event EventHandler? CropRequested;
    public event EventHandler? ShowWindowsRequested;
    public event EventHandler? ArrangeRequested;

    private static void Add(ItemsControl parent, string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        parent.Items.Add(item);
    }
}
