using System.Text.Json.Serialization;

namespace jihub.Github.Models;

public record GitHubIssue
(
    int Id,
    int Number,
    string Title,
    string? Body,
    GitHubMilestone Milestone,
    GithubState State,
    IEnumerable<GitHubLabel> Labels
);
