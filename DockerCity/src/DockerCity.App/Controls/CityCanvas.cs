using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using DockerCity.App.ViewModels;
using DockerCity.App.Views;
using DockerCity.Domain.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.UI;

// Implicit usings bring in System.IO, whose Path would shadow the XAML shape.
using ShapePath = Microsoft.UI.Xaml.Shapes.Path;

namespace DockerCity.App.Controls;

// Why not ItemsControl with a Canvas ItemsPanel?
//
// WinUI wraps every item in a ContentPresenter, so Canvas.Left set inside the
// DataTemplate lands on the wrong element. The usual WPF answer is to bind
// Canvas.Left from ItemContainerStyle, but WinUI does not allow bindings in
// Style setters at all. Rather than fight either limitation, this canvas keeps
// its own children in step with the collections. The view models stay the
// single source of truth; only the plumbing is manual.
//
// Canvas has no ZIndex of its own: children are drawn in the order they were
// added. Three layers, back to front: districts, roads, figures.
public sealed class CityCanvas : Canvas
{
    private const double DragThreshold = 4;

    private readonly Dictionary<ServiceNodeViewModel, FrameworkElement> _nodeVisuals = [];
    private readonly Dictionary<DistrictViewModel, FrameworkElement> _districtVisuals = [];
    private readonly Dictionary<LinkViewModel, ShapePath> _linkVisuals = [];

    private static readonly Color DependsOnColor = Color.FromArgb(255, 214, 218, 228);
    private static readonly Color SharedVolumeColor = Color.FromArgb(255, 78, 176, 150);

    private ServiceNodeViewModel? _dragNode;
    private FrameworkElement? _dragVisual;
    private Point _dragStart;
    private double _originX;
    private double _originY;
    private bool _dragMoved;

    // Raised on press, before any movement, so a plain click still selects.
    public event EventHandler<ServiceNodeViewModel>? NodeSelected;

    // Raised continuously while dragging, so district borders can follow.
    public event EventHandler<ServiceNodeViewModel>? NodeMoving;

    // Raised once on release, and only if the figure actually moved.
    public event EventHandler<ServiceNodeViewModel>? NodeDropped;

    public static readonly DependencyProperty NodesProperty = DependencyProperty.Register(
        nameof(Nodes),
        typeof(ObservableCollection<ServiceNodeViewModel>),
        typeof(CityCanvas),
        new PropertyMetadata(null, OnSourceChanged));

    public static readonly DependencyProperty DistrictsProperty = DependencyProperty.Register(
        nameof(Districts),
        typeof(ObservableCollection<DistrictViewModel>),
        typeof(CityCanvas),
        new PropertyMetadata(null, OnSourceChanged));

    public static readonly DependencyProperty LinksProperty = DependencyProperty.Register(
        nameof(Links),
        typeof(ObservableCollection<LinkViewModel>),
        typeof(CityCanvas),
        new PropertyMetadata(null, OnSourceChanged));

    public ObservableCollection<LinkViewModel>? Links
    {
        get => (ObservableCollection<LinkViewModel>?)GetValue(LinksProperty);
        set => SetValue(LinksProperty, value);
    }

    public static readonly DependencyProperty ShowLinksProperty = DependencyProperty.Register(
        nameof(ShowLinks),
        typeof(bool),
        typeof(CityCanvas),
        new PropertyMetadata(true, OnLayerVisibilityChanged));

    public static readonly DependencyProperty ShowDistrictsProperty = DependencyProperty.Register(
        nameof(ShowDistricts),
        typeof(bool),
        typeof(CityCanvas),
        new PropertyMetadata(true, OnLayerVisibilityChanged));

    public bool ShowLinks
    {
        get => (bool)GetValue(ShowLinksProperty);
        set => SetValue(ShowLinksProperty, value);
    }

    public bool ShowDistricts
    {
        get => (bool)GetValue(ShowDistrictsProperty);
        set => SetValue(ShowDistrictsProperty, value);
    }

    public ObservableCollection<ServiceNodeViewModel>? Nodes
    {
        get => (ObservableCollection<ServiceNodeViewModel>?)GetValue(NodesProperty);
        set => SetValue(NodesProperty, value);
    }

    public ObservableCollection<DistrictViewModel>? Districts
    {
        get => (ObservableCollection<DistrictViewModel>?)GetValue(DistrictsProperty);
        set => SetValue(DistrictsProperty, value);
    }

    private static void OnSourceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var canvas = (CityCanvas)sender;

        if (args.OldValue is INotifyCollectionChanged previous)
        {
            previous.CollectionChanged -= canvas.OnCollectionChanged;
        }

        if (args.NewValue is INotifyCollectionChanged current)
        {
            current.CollectionChanged += canvas.OnCollectionChanged;
        }

        canvas.Rebuild();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args) => Rebuild();

    // Hiding a layer keeps its visuals and subscriptions alive, so turning it
    // back on is instant and the geometry is still current.
    private static void OnLayerVisibilityChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((CityCanvas)sender).ApplyLayerVisibility();

    private void ApplyLayerVisibility()
    {
        var links = ShowLinks ? Visibility.Visible : Visibility.Collapsed;
        var districts = ShowDistricts ? Visibility.Visible : Visibility.Collapsed;

        foreach (var visual in _linkVisuals.Values)
        {
            visual.Visibility = links;
        }

        foreach (var visual in _districtVisuals.Values)
        {
            visual.Visibility = districts;
        }
    }

    private void Rebuild()
    {
        Detach();
        Children.Clear();

        // Order matters: districts, then roads, then figures on top.
        if (Districts is not null)
        {
            foreach (var district in Districts)
            {
                var visual = new DistrictControl { DataContext = district };

                SetLeft(visual, district.X);
                SetTop(visual, district.Y);

                Children.Add(visual);
                _districtVisuals[district] = visual;

                district.PropertyChanged += OnDistrictPropertyChanged;
            }
        }

        if (Links is not null)
        {
            foreach (var link in Links)
            {
                var visual = CreateLinkVisual(link);

                // Geometry is in canvas coordinates, so the shape itself sits
                // at the origin.
                SetLeft(visual, 0);
                SetTop(visual, 0);

                Children.Add(visual);
                _linkVisuals[link] = visual;

                link.PropertyChanged += OnLinkPropertyChanged;
            }
        }

        ApplyLayerVisibility();

        if (Nodes is null)
        {
            return;
        }

        foreach (var node in Nodes)
        {
            var visual = new ServiceNodeControl { DataContext = node };

            SetLeft(visual, node.X);
            SetTop(visual, node.Y);

            visual.PointerPressed += OnNodePointerPressed;
            visual.PointerMoved += OnNodePointerMoved;
            visual.PointerReleased += OnNodePointerReleased;
            visual.PointerCaptureLost += OnNodePointerCaptureLost;

            Children.Add(visual);
            _nodeVisuals[node] = visual;

            node.PropertyChanged += OnNodePropertyChanged;
        }
    }

    private void Detach()
    {
        foreach (var node in _nodeVisuals.Keys)
        {
            node.PropertyChanged -= OnNodePropertyChanged;
        }

        foreach (var district in _districtVisuals.Keys)
        {
            district.PropertyChanged -= OnDistrictPropertyChanged;
        }

        foreach (var link in _linkVisuals.Keys)
        {
            link.PropertyChanged -= OnLinkPropertyChanged;
        }

        _nodeVisuals.Clear();
        _districtVisuals.Clear();
        _linkVisuals.Clear();
    }

    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (sender is not ServiceNodeViewModel node || !_nodeVisuals.TryGetValue(node, out var visual))
        {
            return;
        }

        if (args.PropertyName == nameof(ServiceNodeViewModel.X))
        {
            SetLeft(visual, node.X);
        }
        else if (args.PropertyName == nameof(ServiceNodeViewModel.Y))
        {
            SetTop(visual, node.Y);
        }
    }

    private void OnDistrictPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (sender is not DistrictViewModel district || !_districtVisuals.TryGetValue(district, out var visual))
        {
            return;
        }

        if (args.PropertyName == nameof(DistrictViewModel.X))
        {
            SetLeft(visual, district.X);
        }
        else if (args.PropertyName == nameof(DistrictViewModel.Y))
        {
            SetTop(visual, district.Y);
        }
    }

    private void OnLinkPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (sender is not LinkViewModel link || !_linkVisuals.TryGetValue(link, out var visual))
        {
            return;
        }

        if (args.PropertyName == nameof(LinkViewModel.Route))
        {
            visual.Data = BuildGeometry(link);
        }
        else if (args.PropertyName is nameof(LinkViewModel.Thickness) or nameof(LinkViewModel.Opacity))
        {
            visual.StrokeThickness = link.Thickness;
            visual.Opacity = link.Opacity;
        }
    }

    private static ShapePath CreateLinkVisual(LinkViewModel link)
    {
        var visual = new ShapePath
        {
            // Decoration only; clicks must reach the figures underneath.
            IsHitTestVisible = false,
            Stroke = new SolidColorBrush(link.IsDirected ? DependsOnColor : SharedVolumeColor),
            StrokeThickness = link.Thickness,
            Opacity = link.Opacity,
            Data = BuildGeometry(link)
        };

        if (!link.IsDirected)
        {
            var dashes = new DoubleCollection();
            dashes.Add(4);
            dashes.Add(3);
            visual.StrokeDashArray = dashes;
        }

        return visual;
    }

    private static PathGeometry? BuildGeometry(LinkViewModel link)
    {
        if (link.Route is not LinkPath route)
        {
            return null;
        }

        var road = new PathFigure { StartPoint = ToPoint(route.Start), IsClosed = false };
        road.Segments.Add(new BezierSegment
        {
            Point1 = ToPoint(route.Control1),
            Point2 = ToPoint(route.Control2),
            Point3 = ToPoint(route.End)
        });

        var geometry = new PathGeometry();
        geometry.Figures.Add(road);

        // The head is an open chevron drawn with the same stroke. A filled
        // triangle would need Fill on the Path, which would also fill the
        // area under the curve.
        if (link.IsDirected)
        {
            var head = new PathFigure { StartPoint = ToPoint(route.ArrowLeft), IsClosed = false };
            head.Segments.Add(new LineSegment { Point = ToPoint(route.End) });
            head.Segments.Add(new LineSegment { Point = ToPoint(route.ArrowRight) });
            geometry.Figures.Add(head);
        }

        return geometry;
    }

    private static Point ToPoint(LayoutPoint point) => new(point.X, point.Y);

    private void OnNodePointerPressed(object sender, PointerRoutedEventArgs args)
    {
        if (sender is not FrameworkElement visual || visual.DataContext is not ServiceNodeViewModel node)
        {
            return;
        }

        _dragNode = node;
        _dragVisual = visual;
        _dragStart = args.GetCurrentPoint(this).Position;
        _originX = node.X;
        _originY = node.Y;
        _dragMoved = false;

        visual.CapturePointer(args.Pointer);

        // Selection happens on press, not on release: a drag should also
        // select what is being dragged.
        NodeSelected?.Invoke(this, node);

        args.Handled = true;
    }

    private void OnNodePointerMoved(object sender, PointerRoutedEventArgs args)
    {
        if (_dragNode is null || !ReferenceEquals(sender, _dragVisual))
        {
            return;
        }

        var position = args.GetCurrentPoint(this).Position;
        var deltaX = position.X - _dragStart.X;
        var deltaY = position.Y - _dragStart.Y;

        // Below the threshold this is still a click, not a drag. Without it a
        // shaky hand would turn every click into a stored position.
        if (!_dragMoved && Math.Abs(deltaX) + Math.Abs(deltaY) < DragThreshold)
        {
            return;
        }

        _dragMoved = true;

        _dragNode.X = Math.Max(0, _originX + deltaX);
        _dragNode.Y = Math.Max(0, _originY + deltaY);

        NodeMoving?.Invoke(this, _dragNode);

        args.Handled = true;
    }

    private void OnNodePointerReleased(object sender, PointerRoutedEventArgs args)
    {
        if (_dragVisual is not null)
        {
            _dragVisual.ReleasePointerCapture(args.Pointer);
        }

        EndDrag();
        args.Handled = true;
    }

    private void OnNodePointerCaptureLost(object sender, PointerRoutedEventArgs args) => EndDrag();

    private void EndDrag()
    {
        var node = _dragNode;
        var moved = _dragMoved;

        _dragNode = null;
        _dragVisual = null;
        _dragMoved = false;

        if (node is not null && moved)
        {
            NodeDropped?.Invoke(this, node);
        }
    }
}
