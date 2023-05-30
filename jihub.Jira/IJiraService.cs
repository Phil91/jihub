using jihub.Jira.Models;

namespace jihub.Jira;

public interface IJiraService
{
    Task<IEnumerable<JiraIssue>> GetAsync(string searchQuery, int maxResults, CancellationToken cts);
    Task<(string Hash, string Content)> GetAttachmentAsync(string url, CancellationToken cts);
}