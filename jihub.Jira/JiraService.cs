using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using jihub.Jira.Models;
using Microsoft.Extensions.Logging;

namespace jihub.Jira;

public class JiraService : IJiraService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ILogger<JiraService> _logger;
    private readonly HttpClient _httpClient;
    private readonly HttpClient _httpDownloadClient;

    public JiraService(IHttpClientFactory httpClientFactory, ILogger<JiraService> logger)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient(nameof(JiraService));
        _httpDownloadClient = httpClientFactory.CreateClient($"{nameof(JiraService)}Download");
    }

    public async Task<IEnumerable<JiraIssue>> GetAsync(string searchQuery, int maxResults, CancellationToken cts)
    {
        var result = await RequestJiraIssues(searchQuery, 0, maxResults, cts).ConfigureAwait(false);
        var issues = result.Issues.ToList();
        var pages = Math.Ceiling(((double)result.Total / maxResults));
        if (pages > 1)
        {
            for (var i = 1; i < pages; i++)
            {
                var start = i * maxResults;
                var queryMaxResults = result.Total - issues.Count;
                queryMaxResults = maxResults <= queryMaxResults ? maxResults : queryMaxResults;
                result = await RequestJiraIssues(searchQuery, start, queryMaxResults, cts).ConfigureAwait(false);
                issues.AddRange(result.Issues);
            }
        }

        _logger.LogInformation("Received {Count} Jira Issues", issues.Count);
        return issues;
    }

    private async Task<JiraResult> RequestJiraIssues(string searchQuery, int start, int maxResults, CancellationToken cts)
    {
        var url = $"?jql={searchQuery}&start={start}&maxResults={maxResults}&fields=key,labels,issuetype,project,status,description,summary,components,fixVersions,versions,customfield_10028,customfield_10020,attachment,assignee";

        _logger.LogInformation("Requesting Jira Issues");
        var result = await _httpClient.GetFromJsonAsync<JiraResult>(url, Options, cts).ConfigureAwait(false);
        if (result == null)
        {
            throw new("Jira request failed");
        }

        return result;
    }

    public async Task<(string Hash, string Content)> GetAttachmentAsync(string url, CancellationToken cts)
    {
        var response = await _httpDownloadClient.GetAsync(url, cts).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var sha512Hash = SHA512.Create();
        using var contentStream = await response.Content.ReadAsStreamAsync(cts);
        using var ms = new MemoryStream((int)contentStream.Length);
        await contentStream.CopyToAsync(ms, cts).ConfigureAwait(false);

        var h = await sha512Hash.ComputeHashAsync(ms, cts).ConfigureAwait(false);
        var c = ms.GetBuffer();
        var hash = Convert.ToBase64String(h);
        var content = Convert.ToBase64String(c);
        if (ms.Length != contentStream.Length || c.Length != contentStream.Length)
        {
            throw new($"asset {url.Split("/").Last()} transmitted length {contentStream.Length} doesn't match actual length {ms.Length}.");
        }

        return (hash, content);
    }
}
