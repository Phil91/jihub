using jihub.Github.Models;
using jihub.Github.Services;
using jihub.Jira.Models;
using Microsoft.Extensions.Logging;

namespace jihub.Github;

public class GithubParser : IGithubParser
{
    private readonly ILogger<GithubParser> _logger;
    private readonly IGithubService _githubService;

    public GithubParser(ILogger<GithubParser> logger, IGithubService githubService)
    {
        _logger = logger;
        _githubService = githubService;
    }

    public async Task<IEnumerable<GitHubIssue>> ConvertIssues(IEnumerable<JiraIssue> jiraIssues, string owner, string repo, List<GitHubLabel> labels, List<GitHubMilestone> milestones, CancellationToken cts)
    {
        _logger.LogInformation("converting {Count} jira issues to github issues", jiraIssues.Count());
    
        // spawn tasks that run in parallel
        var tasks = jiraIssues
            .Select(issue => CreateGithubIssue(issue, owner, repo, labels, milestones, cts))
            .ToList();

        await Task.WhenAll(tasks);

        var issues = tasks.Select(task => task.Result).ToList();

        _logger.LogInformation("finish converting issues");
        return issues;
    }
    
    private async Task<GitHubIssue> CreateGithubIssue(JiraIssue jiraIssue, string owner, string repo, List<GitHubLabel> existingLabels, List<GitHubMilestone> milestones, CancellationToken cts)
    {
        var description = jiraIssue.Fields.Description.Replace("\r\n", "<br />").Replace(@"\u{a0}", "");
        var components = jiraIssue.Fields.Components.Any() ? string.Join(",", jiraIssue.Fields.Components.Select(x => x.Name)) : string.Empty;
        if (components != string.Empty)
        {
            description = $"{description}<br/><br/>_Components_: {components}";
        }

        var sprints = jiraIssue.Fields.Sprints.Any() ? string.Join(",", jiraIssue.Fields.Sprints.Select(x => x.Split("name=").Last().Split(",").First().Trim())) : string.Empty;
        if (sprints != string.Empty)
        {
            description = $"{description}<br/><br/>_Sprints_: {sprints}";
        }

        var fixVersions = jiraIssue.Fields.Versions.Any() ? string.Join(",", jiraIssue.Fields.Versions.Select(x => x.Name)) : string.Empty;
        if (fixVersions != string.Empty)
        {
            description = $"{description}<br/><br/>_Fix Versions_: {fixVersions}";
        }

        if (jiraIssue.Fields.StoryPoints != null)
        {
            description = $"{description}<br/><br/>_StoryPoints_: {jiraIssue.Fields.StoryPoints}";
        }

        var labels = jiraIssue.Fields.Labels
            .Select(x => new GitHubLabel(x, string.Empty))
            .Concat(new GitHubLabel[] {
                new(
                    jiraIssue.Fields.Issuetype.Name,
                    jiraIssue.Fields.Issuetype.Description
                )
            });
        var missingLabels = labels
            .Where(l => !existingLabels.Select(el => el.Name.ToLower()).Contains(l.Name.ToLower()))
            .DistinctBy(l => l.Name);
        var createdLabels = await _githubService.CreateLabelsAsync(owner, repo, missingLabels, cts).ConfigureAwait(false);
        existingLabels.AddRange(createdLabels);

        int? milestoneNumber = null;
        if (jiraIssue.Fields.FixVersions.Any())
        {
            var fixVersion = jiraIssue.Fields.FixVersions.Last();
            var milestone = milestones.SingleOrDefault(x => x.Title == fixVersion.Name);
            if (milestone == null)
            {
                milestone = await _githubService.CreateMilestoneAsync(fixVersion.Name, owner, repo, cts).ConfigureAwait(false);
                milestones.Add(milestone);
            }

            milestoneNumber = milestone.Number;
        }

        return new GitHubIssue(
            jiraIssue.Fields.Summary,
            description,
            milestoneNumber,
            labels.Select(x => x.Name)
        );
    }
}