using jihub.Base;
using jihub.Github.Models;

namespace jihub.Parsers;

public interface IParser<in T> where T : class
{
    Task<IEnumerable<CreateGitHubIssue>> ConvertIssues(
        IEnumerable<T> issues,
        JihubOptions options,
        IEnumerable<GithubContent> content,
        List<GitHubLabel> labels,
        List<GitHubMilestone> milestones,
        CancellationToken cts);
}
