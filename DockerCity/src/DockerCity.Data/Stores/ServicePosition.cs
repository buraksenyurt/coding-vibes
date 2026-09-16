namespace DockerCity.Data.Stores;

public readonly record struct ServicePosition(double X, double Y, bool IsPinned = false);
