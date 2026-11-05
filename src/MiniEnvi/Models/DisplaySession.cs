using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MiniEnvi.Processing;

namespace MiniEnvi.Models;

public sealed class DisplaySession
{
    private double _mainAspect = 1;
    private Size _zoomSurfaceSize = new(360, 260);

    public event EventHandler? Changed;
    public WriteableBitmap? Bitmap { get; private set; }
    public int ImageWidth => Bitmap?.PixelWidth ?? 1;
    public int ImageHeight => Bitmap?.PixelHeight ?? 1;
    public Rect MainViewport { get; private set; } = Rect.Empty;
    public Point ZoomCenter { get; private set; }
    public int ZoomFactor { get; private set; } = 8;
    public DisplayMode Mode { get; set; } = DisplayMode.Grayscale;
    public int[] Bands { get; set; } = [0];
    public StretchMode Stretch { get; set; } = StretchMode.Percent2;

    public Rect ZoomViewport
    {
        get
        {
            var width = Math.Max(1, _zoomSurfaceSize.Width / ZoomFactor);
            var height = Math.Max(1, _zoomSurfaceSize.Height / ZoomFactor);
            return ClampRect(new Rect(ZoomCenter.X - width / 2, ZoomCenter.Y - height / 2, width, height));
        }
    }

    public void SetRenderedImage(RenderedImage rendered)
    {
        var bitmap = new WriteableBitmap(rendered.Width, rendered.Height, 96, 96, PixelFormats.Bgra32, null);
        bitmap.WritePixels(new Int32Rect(0, 0, rendered.Width, rendered.Height), rendered.Pixels,
            rendered.Width * 4, 0);
        Bitmap = bitmap;
        var viewHeight = Math.Max(1, rendered.Height * 0.72);
        var viewWidth = Math.Min(rendered.Width, viewHeight * _mainAspect);
        if (viewWidth >= rendered.Width)
        {
            viewWidth = rendered.Width * 0.72;
            viewHeight = Math.Min(rendered.Height, viewWidth / _mainAspect);
        }
        MainViewport = new Rect((rendered.Width - viewWidth) / 2, (rendered.Height - viewHeight) / 2,
            viewWidth, viewHeight);
        ZoomCenter = new Point(rendered.Width / 2.0, rendered.Height / 2.0);
        Notify();
    }

    public void UpdateMainAspect(double aspect)
    {
        if (!double.IsFinite(aspect) || aspect <= 0 || Bitmap is null) return;
        _mainAspect = aspect;
        var center = new Point(MainViewport.X + MainViewport.Width / 2, MainViewport.Y + MainViewport.Height / 2);
        var height = MainViewport.Height <= 0 ? ImageHeight * 0.72 : MainViewport.Height;
        var width = height * aspect;
        if (width > ImageWidth)
        {
            width = ImageWidth;
            height = width / aspect;
        }
        if (height > ImageHeight)
        {
            height = ImageHeight;
            width = height * aspect;
        }
        MainViewport = ClampRect(new Rect(center.X - width / 2, center.Y - height / 2, width, height));
        ZoomCenter = ClampZoomCenterToMain(ZoomCenter);
        Notify();
    }

    public void UpdateZoomSurface(Size size)
    {
        if (size.Width <= 0 || size.Height <= 0) return;
        _zoomSurfaceSize = size;
        ZoomCenter = ClampZoomCenterToMain(ZoomCenter);
        Notify();
    }

    public void MoveMainCenter(Point imagePoint)
    {
        var oldCenter = new Point(MainViewport.X + MainViewport.Width / 2,
            MainViewport.Y + MainViewport.Height / 2);
        MainViewport = ClampRect(new Rect(imagePoint.X - MainViewport.Width / 2,
            imagePoint.Y - MainViewport.Height / 2, MainViewport.Width, MainViewport.Height));
        var newCenter = new Point(MainViewport.X + MainViewport.Width / 2,
            MainViewport.Y + MainViewport.Height / 2);
        ZoomCenter = ClampZoomCenterToMain(new Point(ZoomCenter.X + newCenter.X - oldCenter.X,
            ZoomCenter.Y + newCenter.Y - oldCenter.Y));
        Notify();
    }

    public void MoveZoomCenter(Point imagePoint)
    {
        ZoomCenter = ClampZoomCenterToMain(imagePoint);
        Notify();
    }

    public void ChangeZoom(int direction)
    {
        var levels = new[] { 2, 4, 8, 16, 32 };
        var index = Array.IndexOf(levels, ZoomFactor);
        ZoomFactor = levels[Math.Clamp(index + direction, 0, levels.Length - 1)];
        ZoomCenter = ClampZoomCenterToMain(ZoomCenter);
        Notify();
    }

    private Rect ClampRect(Rect rect)
    {
        var width = Math.Min(rect.Width, ImageWidth);
        var height = Math.Min(rect.Height, ImageHeight);
        var x = Math.Clamp(rect.X, 0, Math.Max(0, ImageWidth - width));
        var y = Math.Clamp(rect.Y, 0, Math.Max(0, ImageHeight - height));
        return new Rect(x, y, width, height);
    }

    private Point ClampPoint(Point point) => new(
        Math.Clamp(point.X, 0, ImageWidth - 1),
        Math.Clamp(point.Y, 0, ImageHeight - 1));

    private Point ClampZoomCenterToMain(Point point)
    {
        point = ClampPoint(point);
        if (MainViewport.IsEmpty) return point;
        var halfWidth = Math.Min(MainViewport.Width, Math.Max(1, _zoomSurfaceSize.Width / ZoomFactor)) / 2;
        var halfHeight = Math.Min(MainViewport.Height, Math.Max(1, _zoomSurfaceSize.Height / ZoomFactor)) / 2;
        return new Point(
            Math.Clamp(point.X, MainViewport.Left + halfWidth, MainViewport.Right - halfWidth),
            Math.Clamp(point.Y, MainViewport.Top + halfHeight, MainViewport.Bottom - halfHeight));
    }

    private void Notify() => Changed?.Invoke(this, EventArgs.Empty);
}
