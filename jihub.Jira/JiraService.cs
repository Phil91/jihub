using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using jihub.Jira.DependencyInjection;
using jihub.Jira.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

    public JiraService(IHttpClientFactory httpClientFactory, ILogger<JiraService> logger)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient(nameof(JiraService));
    }
    
    public async Task<IEnumerable<JiraIssue>> GetAsync(string searchQuery, int maxResults, CancellationToken cts)
    {
        var url = $"?jql={searchQuery}&maxResults={maxResults}&fields=key,labels,issuetype,project,status,description,summary,components,fixVersions,versions,customfield_10028,customfield_10020";

        _logger.LogInformation("Requesting Jira Issues");
        var result = await _httpClient.GetFromJsonAsync<JiraResult>(url, Options, cts).ConfigureAwait(false);
        if (result == null)
        {
            throw new("Jira request failed");
        }

        _logger.LogInformation("Received {Count} Jira Issues", result.Issues.Count());
        return result.Issues;
    }
}