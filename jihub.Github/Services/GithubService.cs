using System.Net;
using jihub.Github.Models;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;

namespace jihub.Github.Services;

public class GithubService : IGithubService
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly ILogger<GithubService> _logger;
    private readonly HttpClient _httpClient;

    public GithubService(IHttpClientFactory httpClientFactory, ILogger<GithubService> logger)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient(nameof(GithubService));
    }
    
    public async Task<GitHubInformation> GetMilestonesAndLabelsAsync(string owner, string repo, CancellationToken cts)
    {
        var labels = await Get<GitHubLabel>("labels", owner, repo, cts).ConfigureAwait(false);
        var milestones = await Get<GitHubMilestone>("milestones", owner, repo, cts).ConfigureAwait(false);

        return new GitHubInformation(
            labels,
            milestones);
    }

    private async Task<IEnumerable<T>> Get<T>(string urlPath, string owner, string repo, CancellationToken cts)
    {
        var url = $"{owner}/{repo}/{urlPath}";
        
        _logger.LogInformation("Requesting {urlPath}", urlPath);
        var result = await _httpClient.GetFromJsonAsync<IEnumerable<T>>(url, Options, cts).ConfigureAwait(false);
        if (result is null)
        {
            throw new("Github request failed");
        }

        _logger.LogInformation("Received {Count} {urlPath}", result.Count(), urlPath);
        return result;
    }

    public async Task<ICollection<GitHubLabel>> CreateLabelsAsync(string owner, string repo, IEnumerable<GitHubLabel> missingLabels, CancellationToken cts)
    {
        async Task<GitHubLabel> CreateLabel(GitHubLabel label)
        {
            var url = $"{owner}/{repo}/labels";
        
            _logger.LogInformation("Creating label: {label}", label.Name);
            var result = await _httpClient.PostAsJsonAsync(url, label, Options, cts).ConfigureAwait(false);
            if (result is null)
            {
                throw new("Jira request failed");
            }

            if (result.StatusCode == HttpStatusCode.Created)
            {
                var content = await result.Content.ReadAsStringAsync(cts).ConfigureAwait(false);
                return JsonSerializer.Deserialize<GitHubLabel>(content, Options) ?? throw new InvalidOperationException();
            }
            
            _logger.LogError("Couldn't create label: {label}", label.Name);
            throw new($"Couldn't create label: {label.Name}");
        }

        var tasks = missingLabels
            .Select(CreateLabel)
            .ToList();

        await Task.WhenAll(tasks).ConfigureAwait(false);

        return tasks.Select(task => task.Result).ToList();
    }

    public async Task<GitHubMilestone> CreateMilestoneAsync(string name, string owner, string repo, CancellationToken cts)
    {
        var url = $"{owner}/{repo}/milestones";
    
        _logger.LogInformation("Creating milestone: {milestone}", name);
        var result = await _httpClient.PostAsJsonAsync(url, new
        {
            title = name
        }, Options, cts).ConfigureAwait(false);
        if (result is null)
        {
            throw new("Milestone creation failed");
        }

        if (result.StatusCode == HttpStatusCode.Created)
        {
            var content = await result.Content.ReadAsStringAsync(cts).ConfigureAwait(false);
            return JsonSerializer.Deserialize<GitHubMilestone>(content, Options) ?? throw new InvalidOperationException();
        }
        
        _logger.LogError("Couldn't create milestone: {milestone}", name);
        throw new($"Couldn't create milestone: {name}");
    }

    public async Task CreateIssuesAsync(string owner, string repo, IEnumerable<GitHubIssue> issues, CancellationToken cts)
    {
        async Task CreateIssue(GitHubIssue issue)
        {
            var url = $"{owner}/{repo}/issues";
        
            _logger.LogInformation("Creating issue: {issue}", issue.Title);
            var result = await _httpClient.PostAsJsonAsync(url, issue, Options, cts).ConfigureAwait(false);
            if (result is null)
            {
                throw new("Jira request failed");
            }

            if (!result.IsSuccessStatusCode)
            {
                _logger.LogError("Couldn't create issue: {label}", issue.Title);
                throw new($"Couldn't create issue: {issue.Title}");
            }
        }

        var tasks = issues
            .Select(CreateIssue)
            .ToList();

        await Task.WhenAll(tasks);
    }

    public async Task<GithubAsset> CreateAttachmentAsync(string owner, string repo, MemoryStream memoryStream, string name, CancellationToken cts)
    {
        var requestContent = new MultipartFormDataContent();
        requestContent.Add(new StreamContent(memoryStream), name.Split(".")[0], name);

        var response = await _httpClient.PostAsync($"{owner}/{repo}/releases/1/assets", requestContent);

        if (!response.IsSuccessStatusCode)
        {
            throw new("Asset creation failed");
        }
        
        var content = await response.Content.ReadAsStringAsync(cts);
        var asset = JsonSerializer.Deserialize<GithubAsset>(content);
        if (asset == null)
        {
            throw new("Couldn't parse Github Asset");
        }

        return asset;
    }
}
