namespace DockerCity.Domain.Layout;

// A cubic Bezier from Start to End plus the two points that, joined through
// End, draw the arrow head. Everything is in canvas coordinates.
public readonly record struct LinkPath(
    LayoutPoint Start,
    LayoutPoint Control1,
    LayoutPoint Control2,
    LayoutPoint End,
    LayoutPoint ArrowLeft,
    LayoutPoint ArrowRight);
