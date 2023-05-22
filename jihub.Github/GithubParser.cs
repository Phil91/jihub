using System.Text.RegularExpressions;
using jihub.Github.Models;
using jihub.Github.Services;
using jihub.Jira;
using jihub.Jira.Models;
using Microsoft.Extensions.Logging;

namespace jihub.Github;

public class GithubParser : IGithubParser
{
    private readonly Regex _regex = new(@"!(.+?)!", RegexOptions.Compiled, TimeSpan.FromSeconds(2));
    private readonly ILogger<GithubParser> _logger;
    private readonly IGithubService _githubService;
    private readonly IJiraService _jiraService;

    public GithubParser(ILogger<GithubParser> logger, IGithubService githubService, IJiraService jiraService)
    {
        _logger = logger;
        _githubService = githubService;
        _jiraService = jiraService;
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
        var matches = _regex.Matches(description);

        var linkedAttachments = new List<ValueTuple<string, GithubAsset>>();
        foreach (Match match in matches)
        {
            var url = match.Groups[1].Value;
            linkedAttachments.Add(new (match.Groups[0].Value, new GithubAsset(url, url.Split("/").Last())));
            // TODO (PS): As soon as github supports uploading a asset to an issue apply code below 
            // var memoryStream = await _jiraService.GetAttachmentAsync(url, cts).ConfigureAwait(false);
            // var asset = await _githubService.CreateAttachmentAsync(owner, repo, memoryStream, url.Split("/").Last(), cts).ConfigureAwait(false);
            // linkedAttachments.Add(new (match.Groups[0].Value, asset));
        }

        description = _regex.Replace(description, x => ReplaceMatch(x, linkedAttachments));

        var components = jiraIssue.Fields.Components.Any() ? string.Join(",", jiraIssue.Fields.Components.Select(x => x.Name)) : string.Empty;
        if (components != string.Empty)
        {
            description = $"{description}<br/><br/>_Components_: {components}";
        }

        var sprints = jiraIssue.Fields.Sprints != null && jiraIssue.Fields.Sprints.Any() ? string.Join(",", jiraIssue.Fields.Sprints.Select(x => x.Split("name=").Last().Split(",").First().Trim())) : string.Empty;
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
            $"{jiraIssue.Key} / {jiraIssue.Fields.Summary}",
            description,
            milestoneNumber,
            labels.Select(x => x.Name)
        );
    }

    private static string ReplaceMatch(Match match, IEnumerable<(string JiraDescriptionUrl, GithubAsset Asset)> linkedAttachments)
    {
        var url = match.Groups[1].Value;
        var matchingAttachment = linkedAttachments.SingleOrDefault(x => x.JiraDescriptionUrl == match.Groups[0].Value);
        return matchingAttachment == default ? 
            $"![{url.Split("/").Last()}]({url})" :
            $"![{matchingAttachment.Asset.Name}]({matchingAttachment.Asset.Url})";
    }
}