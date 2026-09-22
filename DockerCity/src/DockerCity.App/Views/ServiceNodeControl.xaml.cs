using System.ComponentModel;
using System.Numerics;
using DockerCity.App.Services;
using DockerCity.App.ViewModels;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;

// Microsoft.UI.Composition has a CompositionTarget of its own.
using XamlCompositionTarget = Microsoft.UI.Xaml.Media.CompositionTarget;

namespace DockerCity.App.Views;

public sealed partial class ServiceNodeControl : UserControl
{
    private const float HoverScale = 1.08f;

    // One spring per figure, reused: only its final value changes between
    // "grow" and "settle back".
    private SpringVector3NaturalMotionAnimation? _spring;

    private Storyboard? _pulse;
    private bool _pulsing;
    private ServiceNodeViewModel? _node;

    public ServiceNodeControl()
    {
        InitializeComponent();

        // The canvas reuses nothing: a control is built per figure and
        // dropped when the city is rebuilt. Still, the subscription is undone
        // on unload, because a view model outlives its control while a drag
        // is being saved.
        DataContextChanged += (_, args) => Attach(args.NewValue as ServiceNodeViewModel);
        Unloaded += (_, _) => Attach(null);
    }

    private void OnPointerEntered(object sender, PointerRoutedEventArgs args) => SpringTo(HoverScale);

    private void OnPointerExited(object sender, PointerRoutedEventArgs args) => SpringTo(1f);

    // A spring has no duration, only stiffness (Period) and bounciness
    // (DampingRatio). Interrupting it half-way - leaving before it settled -
    // simply retargets it from wherever it is, with no jump.
    private void SpringTo(float scale)
    {
        if (!Motion.IsEnabled)
        {
            return;
        }

        if (_spring is null)
        {
            _spring = XamlCompositionTarget.GetCompositorForCurrentThread().CreateSpringVector3Animation();
            _spring.Target = "Scale";
            _spring.DampingRatio = 0.45f;
            _spring.Period = TimeSpan.FromMilliseconds(50);
        }

        _spring.FinalValue = new Vector3(scale, scale, 1f);

        IconHost.CenterPoint = new Vector3((float)(IconHost.ActualWidth / 2), (float)(IconHost.ActualHeight / 2), 0);
        IconHost.StartAnimation(_spring);
    }

    // ------------------------------------------------------------ Live light

    private void Attach(ServiceNodeViewModel? node)
    {
        if (ReferenceEquals(_node, node))
        {
            return;
        }

        if (_node is not null)
        {
            _node.PropertyChanged -= OnNodeChanged;
        }

        _node = node;

        if (_node is not null)
        {
            _node.PropertyChanged += OnNodeChanged;
        }

        UpdatePulse();
    }

    private void OnNodeChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(ServiceNodeViewModel.IsPulsing))
        {
            UpdatePulse();
        }
    }

    // The light breathes while the container is up. Docker is polled every
    // few seconds, so the animation is started once and left alone until the
    // answer actually changes - restarting it on every poll would make it
    // stutter in time with the polling.
    private void UpdatePulse()
    {
        var shouldPulse = Motion.IsEnabled && _node?.IsPulsing == true;

        if (shouldPulse == _pulsing)
        {
            return;
        }

        _pulsing = shouldPulse;

        if (shouldPulse)
        {
            (_pulse ??= BuildPulse()).Begin();
        }
        else
        {
            _pulse?.Stop();
        }
    }

    private Storyboard BuildPulse()
    {
        var fade = new DoubleAnimation
        {
            From = 1,
            To = 0.35,
            Duration = new Duration(TimeSpan.FromMilliseconds(1100)),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,

            // Stop hands the property back, so the dot returns to full
            // opacity the moment the container stops.
            FillBehavior = FillBehavior.Stop,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };

        Storyboard.SetTarget(fade, LiveDot);
        Storyboard.SetTargetProperty(fade, "Opacity");

        var storyboard = new Storyboard();
        storyboard.Children.Add(fade);

        return storyboard;
    }
}
