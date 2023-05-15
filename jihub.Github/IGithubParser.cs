using jihub.Github.Models;
using jihub.Jira.Models;

namespace jihub.Github;

public interface IGithubParser
{
    Task<IEnumerable<GitHubIssue>> ConvertIssues(
        IEnumerable<JiraIssue> jiraIssues,
        string owner,
        string repo,
        List<GitHubLabel> labels,
        List<GitHubMilestone> milestones,
        CancellationToken cts);
}