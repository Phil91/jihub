using jihub.Github.Models;

namespace jihub.Github.Services;

public interface IGithubService
{
    Task<GitHubInformation> GetRepositoryData(string owner, string repo, CancellationToken cts);
    Task<ICollection<GitHubLabel>> CreateLabelsAsync(string owner, string repo, IEnumerable<GitHubLabel> missingLabels, CancellationToken cts);
    Task<GitHubMilestone> CreateMilestoneAsync(string name, string owner, string repo, CancellationToken cts);
    Task<IEnumerable<GitHubIssue>> CreateIssuesAsync(string owner, string repo, IEnumerable<CreateGitHubIssue> issues, CancellationToken cts);
    Task<Committer> GetCommitter();
    Task<GithubAsset> CreateAttachmentAsync(string owner, string repo, string? importPath, string? branch, (string Hash, string FileContent) fileData, string name, CancellationToken cts);
    Task<IEnumerable<GithubContent>> GetRepoContent(string owner, string repo, string path, CancellationToken cts);

    Task LinkChildren(string owner, string repo, Dictionary<string, List<string>> linkedIssues,
        IEnumerable<GitHubIssue> existingIssues, IEnumerable<GitHubIssue> createdIssues,
        CancellationToken cancellationToken);

    Task AddRelatesComment(string owner, string repo, Dictionary<string, List<string>> relatedIssues,
        IEnumerable<GitHubIssue> existingIssues, IEnumerable<GitHubIssue> createdIssues,
        CancellationToken cancellationToken);
}
