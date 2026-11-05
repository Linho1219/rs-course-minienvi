using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MiniEnvi.IO;
using MiniEnvi.Processing;

namespace MiniEnvi.UI;

public sealed record DisplayRequest(DisplayMode Mode, int[] Bands, StretchMode Stretch);

public sealed class BandSelectionWindow : Window
{
    private readonly ListBox _bands;
    private readonly RadioButton _gray;
    private readonly RadioButton _pseudo;
    private readonly RadioButton _rgb;
    private readonly RadioButton[] _channels;
    private readonly TextBlock[] _channelLabels;
    private readonly ComboBox _stretch;
    private readonly TextBlock _dimensions;
    private EnviDataset? _dataset;
    private readonly int[] _rgbBands = [0, 0, 0];
    private int _grayBand;
    private int _rgbSelections;

    public BandSelectionWindow()
    {
        Title = "Available Bands List";
        Width = 470;
        Height = 690;
        MinWidth = 380;
        MinHeight = 480;

        var root = new DockPanel { Margin = new Thickness(8) };
        var bottom = new StackPanel();
        DockPanel.SetDock(bottom, Dock.Bottom);

        var modeBox = new GroupBox { Header = "显示模式", Margin = new Thickness(0, 8, 0, 5) };
        var modes = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8) };
        _gray = new RadioButton { Content = "灰度", IsChecked = true, Margin = new Thickness(6) };
        _pseudo = new RadioButton { Content = "伪彩色", Margin = new Thickness(6) };
        _rgb = new RadioButton { Content = "RGB 合成", Margin = new Thickness(6) };
        modes.Children.Add(_gray);
        modes.Children.Add(_pseudo);
        modes.Children.Add(_rgb);
        modeBox.Content = modes;
        bottom.Children.Add(modeBox);

        var channelBox = new GroupBox { Header = "当前波段", Margin = new Thickness(0, 2, 0, 5) };
        var channelGrid = new Grid { Margin = new Thickness(8) };
        channelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        channelGrid.ColumnDefinitions.Add(new ColumnDefinition());
        _channels = new RadioButton[3];
        _channelLabels = new TextBlock[3];
        for (var i = 0; i < 3; i++)
        {
            channelGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _channels[i] = new RadioButton
            {
                Content = new[] { "R", "G", "B" }[i],
                GroupName = "RgbChannel",
                IsChecked = i == 0,
                Margin = new Thickness(5)
            };
            _channelLabels[i] = new TextBlock
            {
                Text = "—",
                Margin = new Thickness(8, 5, 5, 5),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetRow(_channels[i], i);
            Grid.SetRow(_channelLabels[i], i);
            Grid.SetColumn(_channelLabels[i], 1);
            channelGrid.Children.Add(_channels[i]);
            channelGrid.Children.Add(_channelLabels[i]);
        }
        _gray.Checked += (_, _) => UpdateModeUi();
        _pseudo.Checked += (_, _) => UpdateModeUi();
        _rgb.Checked += (_, _) => { _rgbSelections = 0; UpdateModeUi(); };
        channelBox.Content = channelGrid;
        bottom.Children.Add(channelBox);

        var stretchRow = new StackPanel { Orientation = Orientation.Horizontal };
        stretchRow.Children.Add(new TextBlock { Text = "显示拉伸：", VerticalAlignment = VerticalAlignment.Center });
        _stretch = new ComboBox { Width = 180 };
        foreach (var mode in Enum.GetValues<StretchMode>()) _stretch.Items.Add(mode.Name());
        _stretch.SelectedIndex = 0;
        stretchRow.Children.Add(_stretch);
        bottom.Children.Add(stretchRow);

        _dimensions = new TextBlock
        {
            Margin = new Thickness(4, 8, 4, 4),
            Padding = new Thickness(6),
            Background = Brushes.WhiteSmoke,
            Text = "尚未打开影像"
        };
        bottom.Children.Add(_dimensions);
        var load = new Button { Content = "加载到 Display #1", HorizontalAlignment = HorizontalAlignment.Stretch };
        load.Click += (_, _) => RaiseDisplayRequest();
        bottom.Children.Add(load);

        _bands = new ListBox { FontFamily = new FontFamily("Consolas"), FontSize = 13 };
        _bands.MouseDoubleClick += BandDoubleClick;
        root.Children.Add(bottom);
        root.Children.Add(_bands);
        Content = root;
    }

    public event EventHandler<DisplayRequest>? DisplayRequested;
    public int SelectedBand => Math.Max(0, _bands.SelectedIndex);
    public DisplayRequest CurrentRequest
    {
        get
        {
            var mode = _rgb.IsChecked == true ? DisplayMode.Rgb :
                _pseudo.IsChecked == true ? DisplayMode.Pseudocolor : DisplayMode.Grayscale;
            var bands = mode == DisplayMode.Rgb ? _rgbBands.ToArray() : new[] { _grayBand };
            return new DisplayRequest(mode, bands, (StretchMode)Math.Max(0, _stretch.SelectedIndex));
        }
    }

    public void SetDataset(EnviDataset dataset)
    {
        _dataset = dataset;
        _bands.Items.Clear();
        for (var i = 0; i < dataset.Header.Bands; i++) _bands.Items.Add(dataset.BandLabel(i));
        _bands.SelectedIndex = 0;
        _grayBand = 0;
        _rgbBands[0] = Math.Min(28, dataset.Header.Bands - 1);
        _rgbBands[1] = Math.Min(19, dataset.Header.Bands - 1);
        _rgbBands[2] = Math.Min(11, dataset.Header.Bands - 1);
        _rgb.IsChecked = dataset.Header.Bands >= 3;
        UpdateChannelLabels();
        UpdateModeUi();
        _dimensions.Text = $"尺寸：{dataset.Header.Samples} × {dataset.Header.Lines} × {dataset.Header.Bands}    " +
                           $"{EnviDataType.NameOf(dataset.Header.DataType)} / {dataset.Header.Interleave.ToUpperInvariant()}";
        Title = $"Available Bands List — {dataset.Name}";
    }

    private void BandDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_dataset is null || _bands.SelectedIndex < 0) return;
        if (_rgb.IsChecked == true)
        {
            var channel = Array.FindIndex(_channels, button => button.IsChecked == true);
            if (channel < 0) channel = 0;
            _rgbBands[channel] = _bands.SelectedIndex;
            _rgbSelections++;
            UpdateChannelLabels();
            _channels[(channel + 1) % 3].IsChecked = true;
            if (_rgbSelections % 3 == 0) RaiseDisplayRequest();
        }
        else
        {
            _grayBand = _bands.SelectedIndex;
            UpdateChannelLabels();
            RaiseDisplayRequest();
        }
    }

    private void RaiseDisplayRequest()
    {
        if (_dataset is null) return;
        DisplayRequested?.Invoke(this, CurrentRequest);
    }

    private void UpdateChannelLabels()
    {
        if (_dataset is null) return;
        for (var i = 0; i < 3; i++) _channelLabels[i].Text = _dataset.BandLabel(_rgbBands[i]);
        if (_rgb.IsChecked != true) _channelLabels[0].Text = _dataset.BandLabel(_grayBand);
    }

    private void UpdateModeUi()
    {
        if (_channels is null || _channelLabels is null) return;
        var rgb = _rgb.IsChecked == true;
        _channels[0].Content = rgb ? "R" : "波段";
        _channels[0].IsChecked = true;
        for (var i = 1; i < 3; i++)
        {
            _channels[i].Visibility = rgb ? Visibility.Visible : Visibility.Collapsed;
            _channelLabels[i].Visibility = rgb ? Visibility.Visible : Visibility.Collapsed;
        }
        UpdateChannelLabels();
    }
}
