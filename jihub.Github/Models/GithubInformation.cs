using System.Text.Json.Serialization;

namespace jihub.Github.Models;

public record GitHubInformation
(
    IEnumerable<GitHubIssue> Issues,
    IEnumerable<GitHubLabel> Labels,
    IEnumerable<GitHubMilestone> Milestones
);

public record GithubContent
(
    string Name,
    [property: JsonPropertyName("html_url")]
    string Url,
    string Type
);

public record GitHubIssue
(
    int Id,
    int Number,
    string Title,
    string? Body,
    GitHubMilestone? Milestone,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    GithubState State,
    IEnumerable<GitHubLabel> Labels
);

public record GitHubLabel
(
    string Name,
    string Description,
    string Color
);

public record GitHubComment(
    string Body
);

public record GitHubMilestone(
    string Title,
    int Number,
    string Description,
    [property: JsonPropertyName("due_on")]
    string? DueOn
);
