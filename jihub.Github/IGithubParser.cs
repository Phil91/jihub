using jihub.Base;
using jihub.Github.Models;
using jihub.Jira.Models;

namespace jihub.Github;

public interface IGithubParser
{
    Task<IEnumerable<GitHubIssue>> ConvertIssues(
        IEnumerable<JiraIssue> jiraIssues,
        JihubOptions options,
        List<GitHubLabel> labels,
        List<GitHubMilestone> milestones,
        CancellationToken cts);
}
