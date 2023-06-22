using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Web;
using jihub.Github.Models;
using Microsoft.Extensions.Logging;

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

    public async Task<IEnumerable<GithubContent>> GetRepoContent(string owner, string repo, CancellationToken cts) =>
        await Get<GithubContent>("contents", owner, repo, cts).ConfigureAwait(false);

    public async Task<GitHubInformation> GetRepositoryData(string owner, string repo, CancellationToken cts)
    {
        var allIssues = new List<GitHubIssue>();
        var page = 1;
        var issuesPerPage = 100;
        while (true)
        {
            var issues = await Get<GitHubIssue>("issues", owner, repo, cts, $"state=all&per_page={100}&page={page}").ConfigureAwait(false);
            if (!issues.Any() || issues.Count() < issuesPerPage)
                break;

            allIssues.AddRange(issues);

            page++;
        }

        var labels = await Get<GitHubLabel>("labels", owner, repo, cts).ConfigureAwait(false);
        var milestones = await Get<GitHubMilestone>("milestones", owner, repo, cts).ConfigureAwait(false);

        return new GitHubInformation(
            allIssues,
            labels,
            milestones);
    }

    private async Task<IEnumerable<T>> Get<T>(string urlPath, string owner, string repo, CancellationToken cts, string? queryParameter = null)
    {
        var url = $"repos/{owner}/{repo}/{urlPath}";
        if (queryParameter != null)
        {
            url = $"{url}?{queryParameter}";
        }

        _logger.LogInformation("Requesting {urlPath}", urlPath);
        var response = await _httpClient.GetAsync(url, cts).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            var jsonResponse = await response.Content.ReadAsStringAsync(cts).ConfigureAwait(false);
            var result = JsonSerializer.Deserialize<IEnumerable<T>>(jsonResponse, Options);
            if (result is null)
            {
                throw new("Github request failed");
            }

            _logger.LogInformation("Received {Count} {urlPath}", result.Count(), urlPath);
            return result;
        }

        _logger.LogError("Couldn't receive {urlPath} because of status code {StatusCode}", urlPath, response.StatusCode);
        return new List<T>();
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
        const int batchSize = 10;
        const int delaySeconds = 20;

        var counter = 1;
        foreach (var issue in issues)
        {
            await CreateIssue(issue, cts).ConfigureAwait(false);
            counter++;
            if (counter % batchSize + 1 != 0)
            {
                continue;
            }

            _logger.LogInformation("Delaying 20 seconds for github to catch some air...");
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cts).ConfigureAwait(false);
            counter = 0;
        }

        async Task CreateIssue(CreateGitHubIssue issue, CancellationToken ct)
        {
            var url = $"repos/{owner}/{repo}/issues";

            _logger.LogInformation("Creating issue: {issue}", issue.Title);
            var result = await _httpClient.PostAsJsonAsync(url, issue, Options, ct).ConfigureAwait(false);
            if (!result.IsSuccessStatusCode)
            {
                _logger.LogError("Couldn't create issue: {label}", issue.Title);
                return;
            }

            if (issue.State == GithubState.Open)
            {
                return;
            }

            var response = await result.Content.ReadFromJsonAsync<GitHubIssue>(Options, ct).ConfigureAwait(false);
            if (response == null)
            {
                _logger.LogError("State of issue {IssueId} could not be changed", issue.Title);
                return;
            }

            var jsonContent = new StringContent("{\"state\":\"closed\"}", Encoding.UTF8, "application/json");
            var updateResult = await _httpClient.PatchAsync($"{url}/{response.Number}", jsonContent, ct).ConfigureAwait(false);
            if (!updateResult.IsSuccessStatusCode)
            {
                _logger.LogError("State of issue {IssueId} could not be changed", issue.Title);
            }
        }
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
