using jihub.Base;
using jihub.Github.Models;
using jihub.Jira.Models;

namespace jihub.Parsers;

public interface IParser<in T> where T : class
{
    Task<IEnumerable<GitHubIssue>> ConvertIssues(
        IEnumerable<T> issues,
        JihubOptions options,
        List<GitHubLabel> labels,
        List<GitHubMilestone> milestones,
        CancellationToken cts);
}