using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using DockerCity.App.ViewModels;
using DockerCity.App.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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
    private readonly Dictionary<ServiceNodeViewModel, FrameworkElement> _nodeVisuals = [];
    private readonly Dictionary<DistrictViewModel, FrameworkElement> _districtVisuals = [];

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
}
