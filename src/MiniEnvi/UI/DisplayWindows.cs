using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Interop;
using MiniEnvi.Models;
using MiniEnvi.UI.Controls;

namespace MiniEnvi.UI;

public sealed class MainDisplayWindow : Window
{
    private readonly TextBlock _status;
    private bool _userIsMoving;

    public MainDisplayWindow(DisplaySession session)
    {
        Title = "Display #1";
        Width = 760;
        Height = 620;
        MinWidth = 360;
        MinHeight = 300;
        _status = new TextBlock { Padding = new Thickness(8, 3, 8, 3), Background = Brushes.WhiteSmoke };
        Surface = new MainImageSurface(session);
        Surface.PixelHovered += (_, point) =>
            _status.Text = $"列 {Math.Floor(point.X) + 1:N0}，行 {Math.Floor(point.Y) + 1:N0}";

        var menu = new Menu();
        var file = new MenuItem { Header = "文件(_F)" };
        var save = new MenuItem { Header = "裁剪并另存..." };
        save.Click += (_, _) => CropRequested?.Invoke(this, EventArgs.Empty);
        file.Items.Add(save);
        var enhance = new MenuItem { Header = "增强(_E)" };
        foreach (var text in new[] { "2% 线性拉伸", "最小—最大拉伸", "均值 ± 2σ", "无拉伸" })
        {
            var item = new MenuItem { Header = text };
            item.Click += (_, _) => StretchRequested?.Invoke(this, text);
            enhance.Items.Add(item);
        }
        var tools = new MenuItem { Header = "工具(_T)" };
        var statistics = new MenuItem { Header = "统计信息..." };
        statistics.Click += (_, _) => StatisticsRequested?.Invoke(this, EventArgs.Empty);
        var metadata = new MenuItem { Header = "元信息..." };
        metadata.Click += (_, _) => MetadataRequested?.Invoke(this, EventArgs.Empty);
        tools.Items.Add(statistics);
        tools.Items.Add(metadata);
        menu.Items.Add(file);
        menu.Items.Add(enhance);
        menu.Items.Add(tools);

        var root = new DockPanel();
        DockPanel.SetDock(menu, Dock.Top);
        DockPanel.SetDock(_status, Dock.Bottom);
        root.Children.Add(menu);
        root.Children.Add(_status);
        root.Children.Add(Surface);
        Content = root;

        SourceInitialized += (_, _) =>
        {
            var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            source?.AddHook(WindowMessageHook);
        };
    }

    public MainImageSurface Surface { get; }
    public bool SuppressAutoArrange { get; set; }
    public event EventHandler? MoveCompleted;
    public event EventHandler? CropRequested;
    public event EventHandler? StatisticsRequested;
    public event EventHandler? MetadataRequested;
    public event EventHandler<string>? StretchRequested;

    private IntPtr WindowMessageHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WmMoving = 0x0216;
        const int WmExitSizeMove = 0x0232;
        if (message == WmMoving) _userIsMoving = true;
        if (message == WmExitSizeMove)
        {
            if (_userIsMoving && !SuppressAutoArrange) MoveCompleted?.Invoke(this, EventArgs.Empty);
            _userIsMoving = false;
        }
        return IntPtr.Zero;
    }
}

public sealed class OverviewWindow : Window
{
    public OverviewWindow(DisplaySession session)
    {
        Title = "Scroll #1";
        Width = 370;
        Height = 310;
        MinWidth = 220;
        MinHeight = 180;
        Content = new OverviewSurface(session);
    }
}

public sealed class ZoomWindow : Window
{
    private readonly TextBlock _factor;

    public ZoomWindow(DisplaySession session)
    {
        Title = "Zoom #1";
        Width = 370;
        Height = 310;
        MinWidth = 220;
        MinHeight = 180;
        var surface = new ZoomSurface(session);
        var root = new Grid();
        root.Children.Add(surface);

        var controls = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(5),
            Background = new SolidColorBrush(Color.FromArgb(180, 20, 20, 20))
        };
        var minus = new Button { Content = "−", FontSize = 16, Padding = new Thickness(7, 0, 7, 1) };
        var plus = new Button { Content = "+", FontSize = 16, Padding = new Thickness(7, 0, 7, 1) };
        _factor = new TextBlock
        {
            Text = $"{session.ZoomFactor}×",
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 6, 0)
        };
        minus.Click += (_, _) => session.ChangeZoom(-1);
        plus.Click += (_, _) => session.ChangeZoom(1);
        session.Changed += (_, _) => _factor.Text = $"{session.ZoomFactor}×";
        controls.Children.Add(minus);
        controls.Children.Add(_factor);
        controls.Children.Add(plus);
        root.Children.Add(controls);
        Content = root;
    }
}
