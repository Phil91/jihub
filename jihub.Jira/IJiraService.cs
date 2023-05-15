using jihub.Jira.Models;

namespace jihub.Jira;

public interface IJiraService
{
    Task<IEnumerable<JiraIssue>> GetAsync(string searchQuery, int maxResults, CancellationToken cts);
}