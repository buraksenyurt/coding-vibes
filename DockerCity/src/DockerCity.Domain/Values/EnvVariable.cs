namespace DockerCity.Domain.Values;

public sealed record EnvVariable(string Key, string? Value)
{
    private static readonly string[] SecretMarkers =
        [
            "PASSWORD"
            , "PASS"
            , "SECRET"
            , "TOKEN"
            , "APIKEY"
            , "API_KEY"
            , "PRIVATE"];

    public bool IsSensitive =>
        SecretMarkers.Any(marker => Key.Contains(marker, StringComparison.OrdinalIgnoreCase));

    public string DisplayValue => IsSensitive ? "*******" : Value ?? "";
}