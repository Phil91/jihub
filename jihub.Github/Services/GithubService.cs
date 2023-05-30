using System.Net;
using jihub.Github.Models;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

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
        var url = $"repos/{owner}/{repo}/{urlPath}";
        
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
            var url = $"repos/{owner}/{repo}/labels";
        
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
        var url = $"repos/{owner}/{repo}/milestones";
    
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

    public async Task CreateIssuesAsync(string owner, string repo, IEnumerable<CreateGitHubIssue> issues, CancellationToken cts)
    {
        async Task CreateIssue(CreateGitHubIssue issue)
        {
            var url = $"repos/{owner}/{repo}/issues";
        
            _logger.LogInformation("Creating issue: {issue}", issue.Title);
            var result = await _httpClient.PostAsJsonAsync(url, issue, Options, cts).ConfigureAwait(false);
            if (!result.IsSuccessStatusCode)
            {
                _logger.LogError("Couldn't create issue: {label}", issue.Title);
                return;
            }

            var response = await result.Content.ReadFromJsonAsync<GitHubIssue>(Options, cts).ConfigureAwait(false);
            if (response == null)
            {
                _logger.LogError("State of issue {IssueId} could not be changed", issue.Title);
                return;
            }

            if (issue.State == GithubState.Closed)
            {
                var updateData = new UpdateGitHubIssue(issue.Title, issue.Body, issue.Milestone, issue.State, issue.Labels);
                var jsonContent = new StringContent(JsonSerializer.Serialize(updateData), Encoding.UTF8, "application/json");
                var updateResult = await _httpClient.PatchAsync($"{url}/{response.Number}", jsonContent, cts).ConfigureAwait(false);
                if (!updateResult.IsSuccessStatusCode)
                {
                    _logger.LogError("State of issue {IssueId} could not be changed", issue.Title);
                }
            }
        }

        var tasks = issues
            .Select(CreateIssue)
            .ToList();

        await Task.WhenAll(tasks);
    }

    public async Task<GithubAsset> CreateAttachmentAsync(string owner, string repo, (string Hash, string FileContent) fileData, string name, CancellationToken cts)
    {
        var url = $"repos/{owner}/{repo}/contents/{name}";
        var content = new UploadFileContent(
            $"Upload file {name}",
            HttpUtility.HtmlEncode(fileData.FileContent)
        );
        var response = await _httpClient.PutAsJsonAsync(url, content, cts).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new("Asset creation failed");
        }

        var assetContent = await response.Content.ReadFromJsonAsync<GithubAssetContent>(Options, cts).ConfigureAwait(false);
        if (assetContent == null)
        {
            throw new("Couldn't parse Github Asset");
        }

        return assetContent.Content;
    }

    public async Task<Committer> GetCommitter()
    {
        var userResponse = await _httpClient.GetFromJsonAsync<GithubUser>("user").ConfigureAwait(false);
        var emailsResponse = await _httpClient.GetFromJsonAsync<IEnumerable<GithubUserEmail>>("user/emails").ConfigureAwait(false);

        if (userResponse == null)
        {
            throw new("Couldn't receive user information");
        }

        if (emailsResponse == null)
        {
            throw new("Couldn't receive user email information");
        }

        var primaryEmails = emailsResponse.Where(x => x.Primary);
        if (primaryEmails.Count() != 1)
        {
            throw new("There must be exactly one primary email");
        }

        return new Committer(userResponse.Name, primaryEmails.Single().Email);
    }
}
