using System.Text.Json.Serialization;

namespace jihub.Github.Models;

public record CreateGitHubIssue
(
    string Title,
    string? Body,
    int? Milestone,
    [property: JsonIgnore]
    GithubState State,
    IEnumerable<string> Labels,
    [property: JsonPropertyName("assignees")]
    IEnumerable<string>? Assignees,
    [property: JsonIgnore]
    IEnumerable<GithubAsset> Attachments
);
