using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using DockerCity.App.ViewModels;
using DockerCity.App.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;

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
// added. Districts are therefore added first so figures land on top of them.
public sealed class CityCanvas : Canvas
{
    private const double DragThreshold = 4;

    private readonly Dictionary<ServiceNodeViewModel, FrameworkElement> _nodeVisuals = [];
    private readonly Dictionary<DistrictViewModel, FrameworkElement> _districtVisuals = [];

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

    private void Rebuild()
    {
        Detach();
        Children.Clear();

        // Order matters: districts first, so they sit behind the figures.
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

        _nodeVisuals.Clear();
        _districtVisuals.Clear();
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
