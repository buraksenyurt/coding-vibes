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
// its own children in step with the collection. The view model stays the
// single source of truth; only the plumbing is manual.
public sealed class CityCanvas : Canvas
{
    private readonly Dictionary<ServiceNodeViewModel, FrameworkElement> _visuals = [];

    public static readonly DependencyProperty NodesProperty = DependencyProperty.Register(
        nameof(Nodes),
        typeof(ObservableCollection<ServiceNodeViewModel>),
        typeof(CityCanvas),
        new PropertyMetadata(null, OnNodesChanged));

    public ObservableCollection<ServiceNodeViewModel>? Nodes
    {
        get => (ObservableCollection<ServiceNodeViewModel>?)GetValue(NodesProperty);
        set => SetValue(NodesProperty, value);
    }

    private static void OnNodesChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var canvas = (CityCanvas)sender;

        if (args.OldValue is ObservableCollection<ServiceNodeViewModel> previous)
        {
            previous.CollectionChanged -= canvas.OnCollectionChanged;
        }

        if (args.NewValue is ObservableCollection<ServiceNodeViewModel> current)
        {
            current.CollectionChanged += canvas.OnCollectionChanged;
        }

        canvas.Rebuild();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args) => Rebuild();

    private void Rebuild()
    {
        foreach (var node in _visuals.Keys)
        {
            node.PropertyChanged -= OnNodePropertyChanged;
        }

        _visuals.Clear();
        Children.Clear();

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
            _visuals[node] = visual;

            node.PropertyChanged += OnNodePropertyChanged;
        }
    }

    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (sender is not ServiceNodeViewModel node || !_visuals.TryGetValue(node, out var visual))
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
}
