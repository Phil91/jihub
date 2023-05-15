namespace jihub.Github.Models;

public record GitHubIssue
(
    string Title,
    string? Body,
    int? Milestone,
    IEnumerable<string> Labels
);
