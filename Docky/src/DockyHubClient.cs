using Spectre.Console;
using System.Text.Json;

namespace DockyApp;

/// <summary>
/// Reads lists from DockyHub. When the hub is not running (connection refused,
/// DNS failure, timeout) it returns <c>null</c> after printing a warning, so the
/// caller can show the built-in catalog instead of failing.
/// </summary>
public static class DockyHubClient
{
    public const string DefaultUrl = "http://localhost:5000";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static async Task<T[]?> TryGetAsync<T>(string hubUrl, string path)
    {
        var url = $"{hubUrl.TrimEnd('/')}/{path}";
        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var response = await httpClient.GetStringAsync(url);
            return JsonSerializer.Deserialize<T[]>(response, JsonOptions) ?? Array.Empty<T>();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            AnsiConsole.MarkupLine($"[yellow]⚠ DockyHub is not reachable at {Markup.Escape(hubUrl)} ({Markup.Escape(ex.Message)}).[/]");
            AnsiConsole.MarkupLine("[yellow]  Showing the catalog built into Docky instead.[/]");
            AnsiConsole.WriteLine();
            return null;
        }
    }
}
