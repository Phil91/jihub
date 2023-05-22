using System.Text.Json.Serialization;

namespace jihub.Jira.Models;

public record JiraResult
(
    int StartAt,
    int MaxResults,
    IEnumerable<JiraIssue> Issues
);

public record JiraIssue(string Key, IssueFields Fields);

public record IssueFields
(
    IssueType Issuetype,
    string Description,
    string Summary,
    IEnumerable<Component> Components,
    Project Project,
    IssueStatus Status,
    IEnumerable<string> Labels,
    IEnumerable<FixVersion> FixVersions,
    IEnumerable<Version> Versions,
    [property: JsonPropertyName("customfield_10028")]
    double? StoryPoints,
    [property: JsonPropertyName("customfield_10020")]
    IEnumerable<string>? Sprints
);

public record IssueType(string Name, string Description);

public record Project(string Key, string Name);

public record IssueStatus(string Name);

public record FixVersion(string Name);

public record Version(string Name);

public record Component(string Name);
