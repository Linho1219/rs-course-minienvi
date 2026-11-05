using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using MiniEnvi.IO;
using MiniEnvi.Processing;

namespace MiniEnvi.UI;

public sealed class MetadataWindow : Window
{
    public MetadataWindow(EnviDataset dataset)
    {
        Title = $"元信息 — {dataset.Name}";
        Width = 700;
        Height = 620;
        var text = new StringBuilder();
        text.AppendLine($"文件：{dataset.DataPath}");
        text.AppendLine($"尺寸：{dataset.Header.Samples} × {dataset.Header.Lines} × {dataset.Header.Bands}");
        text.AppendLine($"数据类型：{EnviDataType.NameOf(dataset.Header.DataType)}");
        text.AppendLine($"交错方式：{dataset.Header.Interleave.ToUpperInvariant()}");
        text.AppendLine($"字节序：{(dataset.Header.ByteOrder == 0 ? "Little Endian" : "Big Endian")}");
        text.AppendLine(new string('─', 70));
        foreach (var (key, value) in dataset.Header.Entries) text.AppendLine($"{key} = {value}");
        Content = new TextBox
        {
            Text = text.ToString(),
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily("Consolas"),
            Margin = new Thickness(8)
        };
    }
}

public sealed class StatisticsWindow : Window
{
    private readonly TextBlock _summary;
    private readonly HistogramControl _histogram;

    public StatisticsWindow(EnviDataset dataset, int band)
    {
        Title = $"统计信息 — {dataset.BandLabel(band)}";
        Width = 680;
        Height = 500;
        _summary = new TextBlock { Margin = new Thickness(12), FontSize = 14, Text = "正在计算..." };
        _histogram = new HistogramControl { Margin = new Thickness(12) };
        var root = new DockPanel();
        DockPanel.SetDock(_summary, Dock.Top);
        root.Children.Add(_summary);
        root.Children.Add(_histogram);
        Content = root;
        Loaded += async (_, _) =>
        {
            try
            {
                var stats = await Task.Run(() => ImageStatistics.Calculate(dataset.ReadBand(band)));
                _summary.Text = string.Create(CultureInfo.InvariantCulture,
                    $"有效像元：{stats.Count:N0}    最小值：{stats.Minimum:G8}    最大值：{stats.Maximum:G8}\n" +
                    $"均值：{stats.Mean:G8}    标准差：{stats.StandardDeviation:G8}");
                _histogram.Values = stats.Histogram;
            }
            catch (Exception ex) { _summary.Text = "统计失败：" + ex.Message; }
        };
    }
}

public sealed class HistogramControl : FrameworkElement
{
    private long[] _values = [];
    public long[] Values { get => _values; set { _values = value; InvalidateVisual(); } }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.White, new Pen(Brushes.Gray, 1), new Rect(RenderSize));
        if (Values.Length == 0 || ActualWidth <= 0 || ActualHeight <= 0) return;
        var max = Math.Max(1, Values.Max());
        var width = ActualWidth / Values.Length;
        for (var i = 0; i < Values.Length; i++)
        {
            var height = Values[i] / (double)max * (ActualHeight - 8);
            dc.DrawRectangle(Brushes.SteelBlue, null,
                new Rect(i * width, ActualHeight - height - 2, Math.Max(1, width), height));
        }
    }
}

public sealed class CropDialog : Window
{
    private readonly EnviDataset _dataset;
    private readonly Int32Rect _region;
    private readonly TextBlock _status;
    private readonly Button _save;

    public CropDialog(EnviDataset dataset, Int32Rect region)
    {
        _dataset = dataset;
        _region = region;
        Title = "矩形裁剪";
        Width = 440;
        Height = 230;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new StackPanel { Margin = new Thickness(18) };
        root.Children.Add(new TextBlock
        {
            Text = $"起始列：{region.X + 1}    起始行：{region.Y + 1}\n" +
                   $"宽度：{region.Width}    高度：{region.Height}\n" +
                   $"输出将保留全部 {dataset.Header.Bands} 个波段。",
            FontSize = 14,
            LineHeight = 25
        });
        _status = new TextBlock { Margin = new Thickness(2, 10, 2, 6), Text = "选择保存位置后生成 HDR + DAT。" };
        _save = new Button { Content = "保存裁剪结果...", HorizontalAlignment = HorizontalAlignment.Right };
        _save.Click += SaveClick;
        root.Children.Add(_status);
        root.Children.Add(_save);
        Content = root;
    }

    private async void SaveClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "ENVI Header (*.hdr)|*.hdr",
            FileName = _dataset.Name + "_crop.hdr",
            AddExtension = true,
            DefaultExt = ".hdr"
        };
        if (dialog.ShowDialog(this) != true) return;
        _save.IsEnabled = false;
        var progress = new Progress<double>(p => _status.Text = $"正在写出：{p:P0}");
        try
        {
            await EnviWriter.WriteCropAsync(_dataset, dialog.FileName, _region.X, _region.Y,
                _region.Width, _region.Height, progress);
            _status.Text = "写出完成，可以重新打开结果检查。";
            MessageBox.Show(this, "裁剪结果已保存。", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _status.Text = "写出失败：" + ex.Message;
            _save.IsEnabled = true;
        }
    }
}
