using System.Numerics;
using DockerCity.App.Services;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

// Microsoft.UI.Composition has a CompositionTarget of its own.
using XamlCompositionTarget = Microsoft.UI.Xaml.Media.CompositionTarget;

namespace DockerCity.App.Views;

public sealed partial class ServiceNodeControl : UserControl
{
    private const float HoverScale = 1.08f;

    // One spring per figure, reused: only its final value changes between
    // "grow" and "settle back".
    private SpringVector3NaturalMotionAnimation? _spring;

    public ServiceNodeControl() => InitializeComponent();

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
}
