using System.ComponentModel;
using System.Windows;
using Microsoft.Win32;
using MiniEnvi.IO;
using MiniEnvi.Models;
using MiniEnvi.Processing;

namespace MiniEnvi.UI;

public sealed class WorkspaceController
{
    private readonly MainMenuWindow _menu = new();
    private readonly BandSelectionWindow _bands = new();
    private readonly DisplaySession _session = new();
    private readonly MainDisplayWindow _main;
    private readonly OverviewWindow _overview;
    private readonly ZoomWindow _zoom;
    private EnviDataset? _dataset;
    private bool _shuttingDown;
    private bool _rendering;

    public WorkspaceController()
    {
        _main = new MainDisplayWindow(_session);
        _overview = new OverviewWindow(_session);
        _zoom = new ZoomWindow(_session);

        _menu.OpenRequested += (_, _) => OpenDataset();
        _menu.CloseDatasetRequested += (_, _) => CloseDataset();
        _menu.MetadataRequested += (_, _) => ShowMetadata();
        _menu.StatisticsRequested += (_, _) => ShowStatistics();
        _menu.CropRequested += (_, _) => BeginCrop();
        _menu.ShowWindowsRequested += (_, _) => ShowAllWindows();
        _menu.ArrangeRequested += (_, _) => ArrangeDisplayTrio();
        _menu.Closing += MenuClosing;

        _bands.DisplayRequested += async (_, request) => await RenderAsync(request);
        _main.MoveCompleted += (_, _) => ArrangeDisplayTrio();
        _main.CropRequested += (_, _) => BeginCrop();
        _main.MetadataRequested += (_, _) => ShowMetadata();
        _main.StatisticsRequested += (_, _) => ShowStatistics();
        _main.StretchRequested += async (_, text) => await ChangeStretchAsync(text);
        _main.Surface.CropSelected += (_, region) => ShowCropDialog(region);

        foreach (var window in new Window[] { _bands, _main, _overview, _zoom })
            window.Closing += HideOnClose;
    }

    public void Show()
    {
        Application.Current.MainWindow = _menu;
        _menu.Show();
    }

    private async void OpenDataset()
    {
        var dialog = new OpenFileDialog
        {
            Title = "打开 ENVI 标准数据",
            Filter = "ENVI Header (*.hdr)|*.hdr|ENVI Data (*.dat;*.img)|*.dat;*.img|所有文件 (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        const string sampleDirectory = @"D:\envi软件与示例数据";
        if (Directory.Exists(sampleDirectory)) dialog.InitialDirectory = sampleDirectory;
        if (dialog.ShowDialog(_menu) != true) return;

        try
        {
            var opened = EnviDataset.Open(dialog.FileName);
            _dataset?.Dispose();
            _dataset = opened;
            _bands.SetDataset(opened);
            _bands.Show();
            _bands.Activate();
            await RenderAsync(_bands.CurrentRequest);
        }
        catch (Exception ex)
        {
            MessageBox.Show(_menu, ex.Message, "打开失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task RenderAsync(DisplayRequest request)
    {
        if (_dataset is null || _rendering) return;
        _rendering = true;
        _bands.IsEnabled = false;
        var oldTitle = _bands.Title;
        _bands.Title = oldTitle + " — 正在渲染...";
        try
        {
            var rendered = await Task.Run(() => StretchRenderer.Render(_dataset, request.Mode,
                request.Bands, request.Stretch));
            _session.Mode = request.Mode;
            _session.Bands = request.Bands;
            _session.Stretch = request.Stretch;
            _session.SetRenderedImage(rendered);
            var labels = string.Join(", ", request.Bands.Select(b => $"Band {b + 1}"));
            _main.Title = $"Display #1 ({labels}) — {_dataset.Name}";
            ShowDisplayWindows();
        }
        catch (Exception ex)
        {
            MessageBox.Show(_bands, ex.Message, "显示失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _bands.IsEnabled = true;
            _bands.Title = oldTitle;
            _rendering = false;
        }
    }

    private async Task ChangeStretchAsync(string text)
    {
        var stretch = text switch
        {
            "最小—最大拉伸" => StretchMode.MinMax,
            "均值 ± 2σ" => StretchMode.StandardDeviation,
            "无拉伸" => StretchMode.None,
            _ => StretchMode.Percent2
        };
        await RenderAsync(new DisplayRequest(_session.Mode, _session.Bands, stretch));
    }

    private void ShowDisplayWindows()
    {
        _main.SuppressAutoArrange = true;
        if (!_main.IsVisible)
        {
            _main.Left = Math.Max(10, _menu.Left);
            _main.Top = _menu.Top + _menu.Height + 8;
            _main.Show();
        }
        if (!_overview.IsVisible) _overview.Show();
        if (!_zoom.IsVisible) _zoom.Show();
        ArrangeDisplayTrio();
        _main.SuppressAutoArrange = false;
    }

    private void ArrangeDisplayTrio()
    {
        if (!_main.IsVisible) return;
        _main.SuppressAutoArrange = true;
        var work = SystemParameters.WorkArea;
        var lowerHeight = Math.Max(_overview.Height, _zoom.Height);
        var mainHeight = _main.ActualHeight > 0 ? _main.ActualHeight : _main.Height;
        _main.Top = Math.Clamp(_main.Top, work.Top,
            Math.Max(work.Top, work.Bottom - mainHeight - lowerHeight - 6));
        var left = Math.Clamp(_main.Left, work.Left,
            Math.Max(work.Left, work.Right - _overview.Width - _zoom.Width - 6));
        _main.Left = Math.Clamp(_main.Left, work.Left, Math.Max(work.Left, work.Right - _main.Width));
        var below = _main.Top + mainHeight + 6;
        _overview.Left = left;
        _overview.Top = below;
        _zoom.Left = Math.Min(work.Right - _zoom.Width, _overview.Left + _overview.ActualWidth + 6);
        _zoom.Top = below;
        _main.SuppressAutoArrange = false;
    }

    private void ShowAllWindows()
    {
        if (_dataset is null)
        {
            MessageBox.Show(_menu, "请先打开 ENVI 数据。", "提示");
            return;
        }
        _bands.Show();
        if (_session.Bitmap is not null) ShowDisplayWindows();
    }

    private void BeginCrop()
    {
        if (_dataset is null || _session.Bitmap is null)
        {
            MessageBox.Show(_menu, "请先打开并显示影像。", "提示");
            return;
        }
        _main.Show();
        _main.Activate();
        _main.Surface.BeginCrop();
        MessageBox.Show(_main, "请在主图中按住鼠标左键拖出裁剪矩形。", "矩形裁剪",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ShowCropDialog(Int32Rect region)
    {
        if (_dataset is null) return;
        new CropDialog(_dataset, region) { Owner = _main }.ShowDialog();
    }

    private void ShowMetadata()
    {
        if (_dataset is null) { MessageBox.Show(_menu, "请先打开 ENVI 数据。", "提示"); return; }
        new MetadataWindow(_dataset) { Owner = _menu }.Show();
    }

    private void ShowStatistics()
    {
        if (_dataset is null) { MessageBox.Show(_menu, "请先打开 ENVI 数据。", "提示"); return; }
        new StatisticsWindow(_dataset, _bands.SelectedBand) { Owner = _menu }.Show();
    }

    private void CloseDataset()
    {
        _main.Hide();
        _overview.Hide();
        _zoom.Hide();
        _bands.Hide();
        _dataset?.Dispose();
        _dataset = null;
    }

    private void HideOnClose(object? sender, CancelEventArgs e)
    {
        if (_shuttingDown) return;
        e.Cancel = true;
        ((Window)sender!).Hide();
    }

    private void MenuClosing(object? sender, CancelEventArgs e)
    {
        _shuttingDown = true;
        _dataset?.Dispose();
        Application.Current.Shutdown();
    }
}
