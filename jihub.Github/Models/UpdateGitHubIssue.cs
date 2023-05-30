namespace jihub.Github.Models;

public record UpdateGitHubIssue
(
    string Title,
    string? Body,
    int? Milestone,
    GithubState State,
    IEnumerable<string> Labels
);
