using System.Text.Json.Serialization;

namespace jihub.Github.Models;

public record GitHubInformation
(
    IEnumerable<GitHubLabel> Labels,
    IEnumerable<GitHubMilestone> Milestones
);

public record GitHubLabel
(
    string Name,
    string Description
);

public record GitHubMilestone(
    string Title,
    int Number,
    string Description,
    [property: JsonPropertyName("due_on")] 
    string? DueOn
);
