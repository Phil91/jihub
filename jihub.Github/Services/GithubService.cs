using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using jihub.Github.Models;
using Microsoft.Extensions.Logging;

namespace jihub.Github.Services;

public class GithubService : IGithubService
{
    private const int batchSize = 10;
    private const int delaySeconds = 20;

    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly ILogger<GithubService> _logger;
    private readonly HttpClient _httpClient;

    public GithubService(IHttpClientFactory httpClientFactory, ILogger<GithubService> logger)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient(nameof(GithubService));
    }

    public async Task<IEnumerable<GithubContent>> GetRepoContent(string owner, string repo, string path, CancellationToken cts)
    {
        var content = await Get<GithubContent>(path, owner, repo, cts).ConfigureAwait(false);
        var files = content.Where(x => x.Type == "file").ToList();
        foreach (var dir in content.Where(x => x.Type == "dir"))
        {
            files.AddRange(await GetRepoContent(owner, repo, $"{path}/{dir.Name}", cts).ConfigureAwait(false));
        }

        return files;
    }

    /// <inheritdoc />
    public async Task LinkChildren(string owner, string repo,
        Dictionary<string, List<string>> linkedIssues,
        IEnumerable<GitHubIssue> existingIssues,
        IEnumerable<GitHubIssue> createdIssues,
        CancellationToken cancellationToken)
    {
        int counter = 0;
        var allIssues = existingIssues.Concat(createdIssues).ToArray();
        foreach (var linkedIssue in linkedIssues)
        {
            if (!linkedIssue.Value.Any())
            {
                continue;
            }

            var matchingIssues = linkedIssue.Value
                .Select(key => createdIssues.FirstOrDefault(i => i.Title.Contains($"(ext: {key})")))
                .Where(i => i != null)
                .Select(i => $"- [ ] #{i!.Number}");
            var issueToUpdate = allIssues.FirstOrDefault(x => x.Title.Contains($"(ext: {linkedIssue.Key})"));
            if (issueToUpdate == null || !matchingIssues.Any())
            {
                continue;
            }

            var updatedBody = issueToUpdate.Body ?? string.Empty;
            if (!updatedBody.Contains("### Children"))
            {
                updatedBody = $"{updatedBody}\n\n### Children";
            }

            updatedBody = $"{updatedBody}\n{string.Join("\n", matchingIssues)}";
            await UpdateIssue(owner, repo, issueToUpdate.Number, new { body = updatedBody }, cancellationToken).ConfigureAwait(false);

            counter++;
            if (counter % batchSize != 0)
            {
                continue;
            }

            _logger.LogInformation("Delaying 20 seconds for github to catch some air...");
            await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken).ConfigureAwait(false);
            counter = 0;
        }
    }

    /// <inheritdoc />
    public async Task AddRelatesComment(string owner, string repo, Dictionary<string, List<string>> relatedIssues, IEnumerable<GitHubIssue> existingIssues,
        IEnumerable<GitHubIssue> createdIssues, CancellationToken cancellationToken)
    {
        async Task CreateComment(GitHubComment comment, int issueNumber)
        {
            var url = $"repos/{owner}/{repo}/issues/{issueNumber}/comments";
            var result = await _httpClient.PostAsJsonAsync(url, comment, Options, cancellationToken).ConfigureAwait(false);
            if (result.StatusCode != HttpStatusCode.Created)
            {
                _logger.LogError("Couldn't create comment: {Body}", comment.Body);
            }
        }

        int counter = 0;
        var allIssues = existingIssues.Concat(createdIssues).ToArray();
        foreach (var relatedIssue in relatedIssues)
        {
            if (!relatedIssue.Value.Any())
            {
                continue;
            }

            var matchingIssues = relatedIssue.Value
                .Select(key => createdIssues.FirstOrDefault(i => i.Title.Contains($"(ext: {key})")))
                .Where(i => i != null)
                .Select(i => $"#{i!.Number}");
            var issueToUpdate = allIssues.FirstOrDefault(x => x.Title.Contains($"(ext: {relatedIssue.Key})"));
            if (issueToUpdate == null || !matchingIssues.Any())
            {
                continue;
            }

            await CreateComment(new GitHubComment($"Relates to: {string.Join(", ", matchingIssues)}"), issueToUpdate.Number).ConfigureAwait(false);

            counter++;
            if (counter % batchSize != 0)
            {
                continue;
            }

            _logger.LogInformation("Delaying 20 seconds for github to catch some air...");
            await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken).ConfigureAwait(false);
            counter = 0;
        }
    }

    public async Task<GitHubInformation> GetRepositoryData(string owner, string repo, CancellationToken cts)
    {
        var allIssues = new List<GitHubIssue>();
        var page = 1;
        const int issuesPerPage = 100;
        while (true)
        {
            var issues = await Get<GitHubIssue>("issues", owner, repo, cts, $"state=all&per_page={issuesPerPage}&page={page}").ConfigureAwait(false);
            allIssues.AddRange(issues);

            if (!issues.Any() || issues.Count() < issuesPerPage)
            {
                break;
            }

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

    public async Task<IEnumerable<GitHubIssue>> CreateIssuesAsync(string owner, string repo, IEnumerable<CreateGitHubIssue> issues, CancellationToken cts)
    {
        var counter = 0;
        var createdIssues = new List<GitHubIssue>();
        foreach (var issue in issues)
        {
            var createdIssue = await CreateIssue(owner, repo, issue, cts).ConfigureAwait(false);
            if (createdIssue != null)
            {
                createdIssues.Add(createdIssue);
            }

            counter++;
            if (counter % batchSize != 0)
            {
                continue;
            }

            _logger.LogInformation("Delaying 20 seconds for github to catch some air...");
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cts).ConfigureAwait(false);
            counter = 0;
        }

        return createdIssues;
    }

    private async Task<GitHubIssue?> CreateIssue(string owner, string repo, CreateGitHubIssue issue, CancellationToken ct)
    {
        var url = $"repos/{owner}/{repo}/issues";
        _logger.LogInformation("Creating issue: {issue}", issue.Title);
        var result = await _httpClient.PostAsJsonAsync(url, issue, Options, ct).ConfigureAwait(false);
        if (!result.IsSuccessStatusCode)
        {
            _logger.LogError("Couldn't create issue: {label}", issue.Title);
            return null;
        }

        var response = await result.Content.ReadFromJsonAsync<GitHubIssue>(Options, ct).ConfigureAwait(false);
        if (response == null)
        {
            _logger.LogError("State of issue {IssueId} could not be changed", issue.Title);
            return null;
        }

        if (issue.State == GithubState.Open)
        {
            return response;
        }

        await UpdateIssue(owner, repo, response.Number, new { state = "closed" }, ct);

        return response;
    }

    private async Task UpdateIssue(string owner, string repo, int id, object data, CancellationToken ct)
    {
        var url = $"repos/{owner}/{repo}/issues/{id}";
        var updateResult = await _httpClient.PatchAsJsonAsync(url, data, Options, ct).ConfigureAwait(false);
        if (!updateResult.IsSuccessStatusCode)
        {
            _logger.LogError("Update of issue {IssueId} failed", id);
        }
    }

    public async Task<GithubAsset> CreateAttachmentAsync(string owner, string repo, string? importPath, string? branch, (string Hash, string FileContent) fileData, string name, CancellationToken cts)
    {
        var directory = importPath == null ? string.Empty : $"{importPath}/";
        var url = $"repos/{owner}/{repo}/contents/{directory}{name}";
        var content = new UploadFileContent(
            $"Upload file {name}",
            HttpUtility.HtmlEncode(fileData.FileContent),
            branch ?? "main"
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

        return new GithubAsset(assetContent.Content.Url.Replace("import-test", "main"), assetContent.Content.Name);
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
