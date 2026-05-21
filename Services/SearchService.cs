using System.Diagnostics;

namespace XboxMetroLauncher.Services;

public sealed class SearchService : ISearchService
{
    public Task SearchWebAsync(string query, string baseSearchUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.CompletedTask;
        }

        var encodedQuery = Uri.EscapeDataString(query.Trim());
        var url = $"{baseSearchUrl}{encodedQuery}";

        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });

        return Task.CompletedTask;
    }
}
