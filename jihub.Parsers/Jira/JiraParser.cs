using System.Runtime.InteropServices.ComTypes;
using System.Text.RegularExpressions;
using jihub.Base;
using jihub.Github.Models;
using jihub.Github.Services;
using jihub.Jira;
using jihub.Jira.Models;
using jihub.Parsers.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace jihub.Parsers.Jira;

public class JiraParser : IJiraParser
{
    private readonly Regex _regex = new(@"!(.+?)!", RegexOptions.Compiled, TimeSpan.FromSeconds(2));
    private readonly ILogger<JiraParser> _logger;
    private readonly IGithubService _githubService;
    private readonly IJiraService _jiraService;
    private readonly JiraParserOptions _options;

    public JiraParser(ILogger<JiraParser> logger, IGithubService githubService, IJiraService jiraService, IOptions<JiraParserOptions> options)
    {
        _logger = logger;
        _githubService = githubService;
        _jiraService = jiraService;
        _options = options.Value;
    }

    public async Task<IEnumerable<CreateGitHubIssue>> ConvertIssues(IEnumerable<JiraIssue> jiraIssues, JihubOptions options, List<GitHubLabel> labels, List<GitHubMilestone> milestones, CancellationToken cts)
    {
        _logger.LogInformation("converting {Count} jira issues to github issues", jiraIssues.Count());
    
        var tasks = jiraIssues
            .Select(issue => CreateGithubIssue(issue, options, labels, milestones, cts))
            .ToList();

        await Task.WhenAll(tasks);

        var issues = tasks.Select(task => task.Result).ToList();

        _logger.LogInformation("finish converting issues");
        return issues;
    }
    
    private async Task<CreateGitHubIssue> CreateGithubIssue(JiraIssue jiraIssue, JihubOptions options, List<GitHubLabel> existingLabels, List<GitHubMilestone> milestones, CancellationToken cts)
    {
        var description = jiraIssue.Fields.Description.Replace("\r\n", "<br />").Replace(@"\u{a0}", "");
        var matches = _regex.Matches(description);

        var linkedAttachments = new List<ValueTuple<string, GithubAsset>>();
        foreach (var groups in matches.Select(m => m.Groups))
        {
            var url = groups[1].Value;
            var fileName = url.Split("/").Last();
            if (!options.Export)
            {
                linkedAttachments.Add(new (groups[0].Value, new GithubAsset(url, fileName)));
                continue;
            }

            // TODO: As soon as github supports uploading a asset to an issue switch to that 
            var fileData = await _jiraService.GetAttachmentAsync(url, cts).ConfigureAwait(false);
            var asset = await _githubService.CreateAttachmentAsync(options.ImportOwner!, options.UploadRepo!, fileData, fileName, cts).ConfigureAwait(false);
            linkedAttachments.Add(new (groups[0].Value, asset));
        }

        description = _regex.Replace(description, x => ReplaceMatch(x, linkedAttachments, options.Link));

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
        var createdLabels = await _githubService.CreateLabelsAsync(options.Owner, options.Repo, missingLabels, cts).ConfigureAwait(false);
        existingLabels.AddRange(createdLabels);
        var state = _options.StateMapping.FirstOrDefault(kvp => kvp.Value.Contains(jiraIssue.Fields.Status.Name)).Key;

        int? milestoneNumber = null;
        if (!jiraIssue.Fields.FixVersions.Any())
        {
            return new CreateGitHubIssue(
                $"{jiraIssue.Fields.Summary} (ext: {jiraIssue.Key})",
                description,
                milestoneNumber,
                state,
                labels.Select(x => x.Name)
            );
        }
        
        var fixVersion = jiraIssue.Fields.FixVersions.Last();
        var milestone = milestones.SingleOrDefault(x => x.Title == fixVersion.Name);
        if (milestone == null)
        {
            milestone = await _githubService.CreateMilestoneAsync(fixVersion.Name, options.Owner, options.Repo, cts)
                .ConfigureAwait(false);
            milestones.Add(milestone);
        }

        milestoneNumber = milestone.Number;

        return new CreateGitHubIssue(
            $"{jiraIssue.Fields.Summary} (ext: {jiraIssue.Key})",
            description,
            milestoneNumber,
            state,
            labels.Select(x => x.Name)
        );
    }

    private static string ReplaceMatch(Match match, IEnumerable<(string JiraDescriptionUrl, GithubAsset Asset)> linkedAttachments, bool authorizedLink)
    {
        var url = match.Groups[1].Value;
        var matchingAttachment = linkedAttachments.SingleOrDefault(x => x.JiraDescriptionUrl == match.Groups[0].Value);
        var linkAsContent = authorizedLink ? string.Empty : "!";
        return matchingAttachment == default ? 
            $"{linkAsContent}[{url.Split("/").Last()}]({url})" :
            $"{linkAsContent}[{matchingAttachment.Asset.Name}]({matchingAttachment.Asset.Url})";
    }
}