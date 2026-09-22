using DockerCity.App.Converters;
using DockerCity.App.ViewModels;
using DockerCity.Domain.Layout;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;

namespace DockerCity.App.Controls;

// A small overview of the whole city with a frame showing what the window
// currently sees. Clicking or dragging on it moves the view there.
//
// It is redrawn from scratch whenever something moves: a city has tens of
// shapes, not thousands, and a full redraw is simpler than keeping two
// canvases in step element by element.
public sealed class MinimapControl : Canvas
{
    private static readonly Color ViewportColor = Color.FromArgb(255, 0, 120, 212);

    private readonly Rectangle _viewport = new()
    {
        Stroke = new SolidColorBrush(ViewportColor),
        StrokeThickness = 1.5,
        Fill = new SolidColorBrush(Color.FromArgb(40, ViewportColor.R, ViewportColor.G, ViewportColor.B)),
        RadiusX = 2,
        RadiusY = 2,
        IsHitTestVisible = false
    };

    private MinimapProjection? _projection;
    private bool _dragging;

    // A point in city coordinates the view should be centred on.
    public event EventHandler<LayoutPoint>? NavigateRequested;

    public MinimapControl()
    {
        // Hit-testable everywhere, not only where a shape happens to be.
        Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += (_, _) => _dragging = false;
    }

    public void Render(
        IEnumerable<DistrictViewModel> districts,
        IEnumerable<ServiceNodeViewModel> nodes,
        LayoutBounds world)
    {
        Children.Clear();

        if (world.IsEmpty || Width is not > 0 || Height is not > 0)
        {
            _projection = null;
            return;
        }

        var projection = new MinimapProjection(world, Width, Height);
        _projection = projection;

        foreach (var district in districts)
        {
            AddBox(projection, district.X, district.Y, district.Width, district.Height, district.Fill, district.Stroke);
        }

        // Only the icon is drawn, not the label under it: at this scale a
        // figure is a coloured dot, and the dot should sit where the icon is.
        var options = LayoutOptions.Default;
        const double iconSize = 72;

        foreach (var node in nodes)
        {
            AddBox(
                projection,
                node.X + options.IconCenterX - (iconSize / 2),
                node.Y + options.IconCenterY - (iconSize / 2),
                iconSize,
                iconSize,
                CategoryToBrushConverter.BrushFor(node.Category),
                stroke: null);
        }

        Children.Add(_viewport);

        Clip = new RectangleGeometry { Rect = new Rect(0, 0, Width, Height) };
    }

    // The visible part of the city, in city coordinates.
    public void ShowViewport(double left, double top, double width, double height)
    {
        if (_projection is null)
        {
            return;
        }

        var topLeft = _projection.ToMinimap(new LayoutPoint(left, top));
        var bottomRight = _projection.ToMinimap(new LayoutPoint(left + width, top + height));

        // The window can see more than the city (empty canvas around it), so
        // the frame is clipped to the panel rather than drawn outside it.
        var x1 = Math.Clamp(topLeft.X, 0, Width);
        var y1 = Math.Clamp(topLeft.Y, 0, Height);
        var x2 = Math.Clamp(bottomRight.X, 0, Width);
        var y2 = Math.Clamp(bottomRight.Y, 0, Height);

        SetLeft(_viewport, x1);
        SetTop(_viewport, y1);
        _viewport.Width = Math.Max(2, x2 - x1);
        _viewport.Height = Math.Max(2, y2 - y1);
    }

    private void AddBox(
        MinimapProjection projection,
        double x,
        double y,
        double width,
        double height,
        Brush fill,
        Brush? stroke)
    {
        var position = projection.ToMinimap(new LayoutPoint(x, y));

        var box = new Rectangle
        {
            // Never smaller than a few pixels, or small services disappear.
            Width = Math.Max(3, width * projection.Scale),
            Height = Math.Max(3, height * projection.Scale),
            Fill = fill,
            Stroke = stroke,
            StrokeThickness = stroke is null ? 0 : 1,
            RadiusX = 2,
            RadiusY = 2,
            IsHitTestVisible = false
        };

        SetLeft(box, position.X);
        SetTop(box, position.Y);
        Children.Add(box);
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs args)
    {
        _dragging = CapturePointer(args.Pointer);
        Navigate(args);
        args.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs args)
    {
        if (_dragging)
        {
            Navigate(args);
            args.Handled = true;
        }
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs args)
    {
        ReleasePointerCapture(args.Pointer);
        _dragging = false;
        args.Handled = true;
    }

    private void Navigate(PointerRoutedEventArgs args)
    {
        if (_projection is null)
        {
            return;
        }

        var point = args.GetCurrentPoint(this).Position;
        NavigateRequested?.Invoke(this, _projection.ToWorld(new LayoutPoint(point.X, point.Y)));
    }
}
