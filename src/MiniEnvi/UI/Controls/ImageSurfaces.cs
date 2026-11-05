using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MiniEnvi.Models;

namespace MiniEnvi.UI.Controls;

public abstract class ImageSurfaceBase : FrameworkElement
{
    protected ImageSurfaceBase(DisplaySession session)
    {
        Session = session;
        Session.Changed += (_, _) => InvalidateVisual();
        SnapsToDevicePixels = true;
        Focusable = true;
    }

    protected DisplaySession Session { get; }

    protected Rect FittedImageRect
    {
        get
        {
            if (Session.Bitmap is null || ActualWidth <= 0 || ActualHeight <= 0) return Rect.Empty;
            var scale = Math.Min(ActualWidth / Session.ImageWidth, ActualHeight / Session.ImageHeight);
            var width = Session.ImageWidth * scale;
            var height = Session.ImageHeight * scale;
            return new Rect((ActualWidth - width) / 2, (ActualHeight - height) / 2, width, height);
        }
    }

    protected Point FullScreenToImage(Point point)
    {
        var rect = FittedImageRect;
        if (rect.IsEmpty) return new Point();
        return new Point(
            Math.Clamp((point.X - rect.X) / rect.Width * Session.ImageWidth, 0, Session.ImageWidth - 1),
            Math.Clamp((point.Y - rect.Y) / rect.Height * Session.ImageHeight, 0, Session.ImageHeight - 1));
    }

    protected Rect FullImageToScreen(Rect imageRect)
    {
        var rect = FittedImageRect;
        return new Rect(
            rect.X + imageRect.X / Session.ImageWidth * rect.Width,
            rect.Y + imageRect.Y / Session.ImageHeight * rect.Height,
            imageRect.Width / Session.ImageWidth * rect.Width,
            imageRect.Height / Session.ImageHeight * rect.Height);
    }

    protected static void DrawRedBox(DrawingContext dc, Rect rect)
    {
        dc.DrawRectangle(null, new Pen(Brushes.Red, 2), rect);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        dc.DrawRectangle(Brushes.Black, null, new Rect(RenderSize));
    }
}

public sealed class OverviewSurface : ImageSurfaceBase
{
    private bool _dragging;

    public OverviewSurface(DisplaySession session) : base(session)
    {
        Cursor = Cursors.Cross;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (Session.Bitmap is null) return;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        dc.DrawImage(Session.Bitmap, FittedImageRect);
        DrawRedBox(dc, FullImageToScreen(Session.MainViewport));
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        _dragging = true;
        CaptureMouse();
        Session.MoveMainCenter(FullScreenToImage(e.GetPosition(this)));
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging && e.LeftButton == MouseButtonState.Pressed)
            Session.MoveMainCenter(FullScreenToImage(e.GetPosition(this)));
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        _dragging = false;
        ReleaseMouseCapture();
        base.OnMouseLeftButtonUp(e);
    }
}

public sealed class MainImageSurface : ImageSurfaceBase
{
    private Point? _cropStart;
    private Point? _cropCurrent;
    private bool _draggingZoom;

    public MainImageSurface(DisplaySession session) : base(session)
    {
        Cursor = Cursors.Cross;
        SizeChanged += (_, _) =>
        {
            if (ActualHeight > 0) Session.UpdateMainAspect(ActualWidth / ActualHeight);
        };
    }

    public bool CropMode { get; private set; }
    public event EventHandler<Int32Rect>? CropSelected;
    public event EventHandler<Point>? PixelHovered;

    public void BeginCrop()
    {
        CropMode = true;
        _cropStart = _cropCurrent = null;
        Cursor = Cursors.Cross;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (Session.Bitmap is null || Session.MainViewport.IsEmpty) return;
        var brush = new ImageBrush(Session.Bitmap)
        {
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewbox = Session.MainViewport,
            Stretch = Stretch.Fill
        };
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        dc.DrawRectangle(brush, null, new Rect(RenderSize));
        DrawRedBox(dc, ViewImageToScreen(Session.ZoomViewport));

        if (_cropStart is { } start && _cropCurrent is { } current)
        {
            var rect = Normalize(new Rect(start, current));
            var pen = new Pen(Brushes.Yellow, 2) { DashStyle = DashStyles.Dash };
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(35, 255, 255, 0)), pen, rect);
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        CaptureMouse();
        if (CropMode)
            _cropStart = _cropCurrent = e.GetPosition(this);
        else
        {
            _draggingZoom = true;
            Session.MoveZoomCenter(ViewScreenToImage(e.GetPosition(this)));
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var point = e.GetPosition(this);
        PixelHovered?.Invoke(this, ViewScreenToImage(point));
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (CropMode && _cropStart is not null)
        {
            _cropCurrent = point;
            InvalidateVisual();
        }
        else if (_draggingZoom)
            Session.MoveZoomCenter(ViewScreenToImage(point));
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (CropMode && _cropStart is { } start)
        {
            var end = e.GetPosition(this);
            var a = ViewScreenToImage(start);
            var b = ViewScreenToImage(end);
            var x = (int)Math.Floor(Math.Min(a.X, b.X));
            var y = (int)Math.Floor(Math.Min(a.Y, b.Y));
            var right = (int)Math.Ceiling(Math.Max(a.X, b.X));
            var bottom = (int)Math.Ceiling(Math.Max(a.Y, b.Y));
            var selection = new Int32Rect(x, y,
                Math.Clamp(right - x, 1, Session.ImageWidth - x),
                Math.Clamp(bottom - y, 1, Session.ImageHeight - y));
            CropMode = false;
            _cropStart = _cropCurrent = null;
            CropSelected?.Invoke(this, selection);
        }
        _draggingZoom = false;
        ReleaseMouseCapture();
        InvalidateVisual();
        base.OnMouseLeftButtonUp(e);
    }

    private Point ViewScreenToImage(Point point)
    {
        var view = Session.MainViewport;
        if (ActualWidth <= 0 || ActualHeight <= 0) return new Point(view.X, view.Y);
        return new Point(
            Math.Clamp(view.X + point.X / ActualWidth * view.Width, 0, Session.ImageWidth - 1),
            Math.Clamp(view.Y + point.Y / ActualHeight * view.Height, 0, Session.ImageHeight - 1));
    }

    private Rect ViewImageToScreen(Rect image)
    {
        var view = Session.MainViewport;
        return new Rect(
            (image.X - view.X) / view.Width * ActualWidth,
            (image.Y - view.Y) / view.Height * ActualHeight,
            image.Width / view.Width * ActualWidth,
            image.Height / view.Height * ActualHeight);
    }

    private static Rect Normalize(Rect rect) => new(
        Math.Min(rect.Left, rect.Right), Math.Min(rect.Top, rect.Bottom),
        Math.Abs(rect.Width), Math.Abs(rect.Height));
}

public sealed class ZoomSurface : ImageSurfaceBase
{
    public ZoomSurface(DisplaySession session) : base(session)
    {
        SizeChanged += (_, _) => Session.UpdateZoomSurface(RenderSize);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (Session.Bitmap is null) return;
        var brush = new ImageBrush(Session.Bitmap)
        {
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewbox = Session.ZoomViewport,
            Stretch = Stretch.Fill
        };
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
        dc.DrawRectangle(brush, null, new Rect(RenderSize));
    }
}
