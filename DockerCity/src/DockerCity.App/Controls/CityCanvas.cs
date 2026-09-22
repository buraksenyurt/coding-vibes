using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Numerics;
using DockerCity.App.Services;
using DockerCity.App.ViewModels;
using DockerCity.App.Views;
using DockerCity.Domain.Layout;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Foundation;
using Windows.UI;

// Implicit usings bring in System.IO, whose Path would shadow the XAML shape.
using ShapePath = Microsoft.UI.Xaml.Shapes.Path;

// Microsoft.UI.Composition has a CompositionTarget of its own.
using XamlCompositionTarget = Microsoft.UI.Xaml.Media.CompositionTarget;

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

    // Entrance timing, in milliseconds. The whole show stays under a second
    // however many services there are: the step shrinks as the city grows.
    private const double DistrictStep = 60;
    private const double DistrictDuration = 350;
    private const double FirstNodeDelay = 150;
    private const double MaxNodeStep = 45;
    private const double NodeSpread = 700;
    private const double NodeDuration = 420;
    private const double RoadFade = 300;

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

    private readonly DispatcherQueueTimer _roadsTimer;

    public CityCanvas()
    {
        // A Canvas without a background is invisible to the pointer wherever
        // it has no children, so clicks on empty ground fell through to the
        // frame behind it. Transparent is enough to make it hit-testable.
        Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));

        _roadsTimer = DispatcherQueue.CreateTimer();
        _roadsTimer.IsRepeating = false;
        _roadsTimer.Tick += (_, _) => FadeRoadsIn();
    }

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

    // Adding is by far the common case (every load adds one item at a time),
    // so it is handled in place. Anything else - a Clear, a Remove - is rare
    // enough that starting over is simpler than being clever.
    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (args.Action != NotifyCollectionChangedAction.Add || args.NewItems is null)
        {
            Rebuild();
            return;
        }

        foreach (var item in args.NewItems)
        {
            switch (item)
            {
                case DistrictViewModel district:
                    AddDistrict(district);
                    break;
                case LinkViewModel link:
                    AddLink(link);
                    break;
                case ServiceNodeViewModel node:
                    AddNode(node);
                    break;
            }
        }
    }

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
        _roadsTimer.Stop();
        Detach();
        Children.Clear();

        foreach (var district in Districts ?? [])
        {
            AddDistrict(district);
        }

        foreach (var link in Links ?? [])
        {
            AddLink(link);
        }

        foreach (var node in Nodes ?? [])
        {
            AddNode(node);
        }
    }

    // Each layer is inserted at its own depth, whatever order the items come
    // in: districts at the back, roads above them, figures on top.
    private void AddDistrict(DistrictViewModel district)
    {
        var visual = new DistrictControl
        {
            DataContext = district,
            Visibility = ShowDistricts ? Visibility.Visible : Visibility.Collapsed
        };

        SetLeft(visual, district.X);
        SetTop(visual, district.Y);

        Children.Insert(_districtVisuals.Count, visual);
        _districtVisuals[district] = visual;

        district.PropertyChanged += OnDistrictPropertyChanged;
    }

    private void AddLink(LinkViewModel link)
    {
        var visual = CreateLinkVisual(link);
        visual.Visibility = ShowLinks ? Visibility.Visible : Visibility.Collapsed;

        // Geometry is in canvas coordinates, so the shape itself sits at the
        // origin.
        SetLeft(visual, 0);
        SetTop(visual, 0);

        Children.Insert(_districtVisuals.Count + _linkVisuals.Count, visual);
        _linkVisuals[link] = visual;

        link.PropertyChanged += OnLinkPropertyChanged;
    }

    private void AddNode(ServiceNodeViewModel node)
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

    // ------------------------------------------------------------ Entrance

    // Districts grow out of the ground, figures pop up one after another in
    // reading order, and the roads are laid last, once there is something to
    // connect. Composition animations run on the compositor thread, so a busy
    // UI thread does not make them stutter.
    public void PlayEntrance()
    {
        if (!Motion.IsEnabled || Children.Count == 0)
        {
            return;
        }

        var compositor = XamlCompositionTarget.GetCompositorForCurrentThread();

        // "Ease out back": overshoots a little and settles, which is what makes
        // it read as a bounce rather than a slide.
        var easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.34f, 1.56f), new Vector2(0.64f, 1f));

        var order = 0;

        foreach (var (district, visual) in _districtVisuals)
        {
            visual.CenterPoint = new Vector3((float)(district.Width / 2), (float)(district.Height / 2), 0);
            Pop(compositor, easing, visual, order++ * DistrictStep, DistrictDuration, rise: 0);
        }

        var nodes = _nodeVisuals
            .OrderBy(pair => pair.Key.Y)
            .ThenBy(pair => pair.Key.X)
            .Select(pair => pair.Value)
            .ToList();

        var step = nodes.Count <= 1 ? 0 : Math.Min(MaxNodeStep, NodeSpread / (nodes.Count - 1));
        var icon = LayoutOptions.Default;

        for (var index = 0; index < nodes.Count; index++)
        {
            // Grow from the icon, not from the middle of icon plus labels.
            nodes[index].CenterPoint = new Vector3((float)icon.IconCenterX, (float)icon.IconCenterY, 0);
            Pop(compositor, easing, nodes[index], FirstNodeDelay + (index * step), NodeDuration, rise: 18);
        }

        if (_linkVisuals.Count == 0 || !ShowLinks)
        {
            return;
        }

        foreach (var visual in _linkVisuals.Values)
        {
            visual.Visibility = Visibility.Collapsed;
        }

        var lastLanding = FirstNodeDelay + (Math.Max(0, nodes.Count - 1) * step) + NodeDuration;
        _roadsTimer.Interval = TimeSpan.FromMilliseconds(lastLanding);
        _roadsTimer.Start();
    }

    private static void Pop(
        Compositor compositor,
        CompositionEasingFunction easing,
        UIElement visual,
        double delayMs,
        double durationMs,
        float rise)
    {
        var delay = TimeSpan.FromMilliseconds(delayMs);
        var duration = TimeSpan.FromMilliseconds(durationMs);

        // Scale and Translation are XAML properties backed by the compositor
        // (the "facades"), so UIElement.StartAnimation can drive them directly.
        // Both are otherwise unused on these elements, so nothing fights over
        // them, and both end on their default values.
        var scale = compositor.CreateVector3KeyFrameAnimation();
        scale.Target = "Scale";
        scale.InsertKeyFrame(0f, new Vector3(0.01f, 0.01f, 1f));
        scale.InsertKeyFrame(1f, Vector3.One, easing);
        scale.Duration = duration;
        scale.DelayTime = delay;

        // Without this the element would sit at full size until its turn came,
        // then collapse and grow. With it, it waits invisibly at key frame 0.
        scale.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        visual.StartAnimation(scale);

        if (rise <= 0)
        {
            return;
        }

        var translation = compositor.CreateVector3KeyFrameAnimation();
        translation.Target = "Translation";
        translation.InsertKeyFrame(0f, new Vector3(0, rise, 0));
        translation.InsertKeyFrame(1f, Vector3.Zero, easing);
        translation.Duration = duration;
        translation.DelayTime = delay;
        translation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        visual.StartAnimation(translation);
    }

    // Roads have no Scale to play with, and their Opacity belongs to the
    // selection highlight. A storyboard with FillBehavior.Stop borrows the
    // property for the fade and hands it back untouched when it ends.
    private void FadeRoadsIn()
    {
        ApplyLayerVisibility();

        if (!ShowLinks)
        {
            return;
        }

        var storyboard = new Storyboard();

        foreach (var visual in _linkVisuals.Values)
        {
            var fade = new DoubleAnimation
            {
                From = 0,
                To = visual.Opacity,
                Duration = new Duration(TimeSpan.FromMilliseconds(RoadFade)),
                FillBehavior = FillBehavior.Stop
            };

            Storyboard.SetTarget(fade, visual);
            Storyboard.SetTargetProperty(fade, nameof(Opacity));
            storyboard.Children.Add(fade);
        }

        storyboard.Begin();
    }

    // ------------------------------------------------------------ Pan cursor

    // ProtectedCursor can only be set from inside the element's own class,
    // which is why the window asks rather than setting it itself.
    public void SetPanCursor(bool panning) =>
        ProtectedCursor = panning ? InputSystemCursor.Create(InputSystemCursorShape.SizeAll) : null;

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
