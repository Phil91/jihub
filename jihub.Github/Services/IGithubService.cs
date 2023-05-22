using jihub.Github.Models;

namespace jihub.Github.Services;

public interface IGithubService
{
    Task<GitHubInformation> GetMilestonesAndLabelsAsync(string owner, string repo, CancellationToken cts);
    Task<ICollection<GitHubLabel>> CreateLabelsAsync(string owner, string repo, IEnumerable<GitHubLabel> missingLabels, CancellationToken cts);
    Task<GitHubMilestone> CreateMilestoneAsync(string name, string owner, string repo, CancellationToken cts);
    Task CreateIssuesAsync(string owner, string repo, IEnumerable<GitHubIssue> issues, CancellationToken cts);
    Task<GithubAsset> CreateAttachmentAsync(string owner, string repo, MemoryStream memoryStream, string name, CancellationToken cts);
}