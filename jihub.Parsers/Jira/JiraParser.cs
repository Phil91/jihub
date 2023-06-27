using System.Text;
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
    private readonly Regex _linkRegex = new(@"\[.{1,255}\](?!\([^)]*\))", RegexOptions.Compiled, TimeSpan.FromSeconds(2));

    private readonly ILogger<JiraParser> _logger;
    private readonly IGithubService _githubService;
    private readonly IJiraService _jiraService;
    private readonly ParserSettings _settings;

    public JiraParser(ILogger<JiraParser> logger, IGithubService githubService, IJiraService jiraService, IOptions<ParserSettings> options)
    {
        _logger = logger;
        _githubService = githubService;
        _jiraService = jiraService;
        _settings = options.Value;
    }

    public async Task<IEnumerable<CreateGitHubIssue>> ConvertIssues(IEnumerable<JiraIssue> jiraIssues, JihubOptions options, IEnumerable<GithubContent> content, List<GitHubLabel> labels, List<GitHubMilestone> milestones, CancellationToken cts)
    {
        _logger.LogInformation("converting {Count} jira issues to github issues", jiraIssues.Count());

        var issues = new List<CreateGitHubIssue>();
        foreach (var issue in jiraIssues)
        {
            issues.Add(await CreateGithubIssue(issue, options, content, labels, milestones, cts).ConfigureAwait(false));
        }

        _logger.LogInformation("finish converting issues");
        return issues;
    }

    private async Task<CreateGitHubIssue> CreateGithubIssue(JiraIssue jiraIssue, JihubOptions options, IEnumerable<GithubContent> content, List<GitHubLabel> existingLabels, List<GitHubMilestone> milestones, CancellationToken cts)
    {
        var (assets, linkedAttachments) = await HandleAttachments(jiraIssue, options, content, jiraIssue.Fields.Description, cts).ConfigureAwait(false);
        var description = GetDescription(jiraIssue, options, linkedAttachments, assets);
        var labels = await GetGithubLabels(jiraIssue, options, existingLabels, cts).ConfigureAwait(false);
        var state = GetGithubState(jiraIssue);
        var milestoneNumber = await GetMilestoneNumber(jiraIssue, options, milestones, cts).ConfigureAwait(false);
        var mailMapping = _settings.Jira.EmailMappings.SingleOrDefault(x => x.JiraMail.Equals(jiraIssue.Fields.Assignee.Name, StringComparison.OrdinalIgnoreCase));
        var assignee = mailMapping != null ? Enumerable.Repeat(mailMapping.GithubName, 1) : Enumerable.Empty<string>();

        return new CreateGitHubIssue(
            $"{jiraIssue.Fields.Summary} (ext: {jiraIssue.Key})",
            description,
            milestoneNumber,
            state,
            labels.Select(x => x.Name),
            assignee,
            assets
        );
    }

    private async Task<(List<GithubAsset>, List<(string, GithubAsset)> linkedAttachments)> HandleAttachments(JiraIssue jiraIssue, JihubOptions options, IEnumerable<GithubContent> githubContent, string description, CancellationToken cts)
    {
        var matches = _regex.Matches(description);
        var assets = new List<GithubAsset>();
        foreach (var attachment in jiraIssue.Fields.Attachment)
        {
            var content = githubContent.SingleOrDefault(x => x.Name.Equals(attachment.Filename, StringComparison.OrdinalIgnoreCase));
            if (!options.Export || content != null)
            {
                assets.Add(new GithubAsset(content == null ? attachment.Url : content.Url, attachment.Filename));
                continue;
            }

            var fileData = await _jiraService.GetAttachmentAsync(attachment.Url, cts).ConfigureAwait(false);
            try
            {
                var asset = await _githubService
                    .CreateAttachmentAsync(options.ImportOwner!, options.UploadRepo!, fileData, attachment.Filename,
                        cts)
                    .ConfigureAwait(false);
                assets.Add(asset);
            }
            catch
            {
                _logger.LogError("Couldn't create asset: {AssetName}", attachment.Filename);
            }
        }

        var linkedAttachments = new List<ValueTuple<string, GithubAsset>>();
        if (!matches.Any())
        {
            return (assets, linkedAttachments);
        }

        foreach (var groups in _regex.Matches(description).Select(m => m.Groups))
        {
            var asset = assets.Find(x => groups[1].Value.Contains(x.Name));
            if (asset == null)
            {
                _logger.LogError("Asset {AssetName} couldn't be found", groups[1].Value);
            }

            linkedAttachments.Add(new(groups[0].Value, asset!));
            assets.Remove(asset!);
        }

        return (assets, linkedAttachments);
    }

    private string GetDescription(JiraIssue jiraIssue, JihubOptions options, ICollection<(string, GithubAsset)> attachmentsToReplace, IEnumerable<GithubAsset> assets)
    {
        var description = _regex.Replace(jiraIssue.Fields.Description.Replace(@"\u{a0}", ""),
            x => ReplaceMatch(x, attachmentsToReplace, options.Link));
        description = _linkRegex.Replace(description, ReplaceLinks);
        var components = jiraIssue.Fields.Components.Any() ?
            string.Join(",", jiraIssue.Fields.Components.Select(x => x.Name)) :
            "N/A";
        var sprints = jiraIssue.Fields.Sprints != null && jiraIssue.Fields.Sprints.Any() ?
            string.Join(",", jiraIssue.Fields.Sprints.Select(x => x.Split("name=").LastOrDefault()?.Split(",").FirstOrDefault()?.Trim())) :
            "N/A";
        var fixVersions = jiraIssue.Fields.Versions.Any() ?
            string.Join(",", jiraIssue.Fields.Versions.Select(x => x.Name)) :
            "N/A";
        var storyPoints = jiraIssue.Fields.StoryPoints == null ?
            "N/A"
            : jiraIssue.Fields.StoryPoints.ToString();

        var linkAsContent = options.Link ? string.Empty : "!";
        var attachments = attachmentsToReplace.Any() ?
            string.Join(", ", assets.Select(a => $"{linkAsContent}[{a.Name}]({a.Url})")) :
            "N/A";

        return _settings.Jira.DescriptionTemplate
            .Replace("{{Description}}", description)
            .Replace("{{Components}}", components)
            .Replace("{{Sprints}}", sprints)
            .Replace("{{FixVersions}}", fixVersions)
            .Replace("{{StoryPoints}}", storyPoints)
            .Replace("{{Attachments}}", attachments);
    }

    private async Task<IEnumerable<GitHubLabel>> GetGithubLabels(JiraIssue jiraIssue, JihubOptions options, List<GitHubLabel> existingLabels, CancellationToken cts)
    {
        var labels = jiraIssue.Fields.Labels
            .Select(x => new GitHubLabel(x, string.Empty))
            .Concat(new GitHubLabel[]
            {
                new(
                    jiraIssue.Fields.Issuetype.Name,
                    jiraIssue.Fields.Issuetype.Description
                )
            });
        var missingLabels = labels
            .Where(l => !existingLabels.Select(el => el.Name)
                .Any(el => el.Equals(l.Name, StringComparison.OrdinalIgnoreCase)))
            .DistinctBy(l => l.Name);
        var createdLabels = await _githubService.CreateLabelsAsync(options.Owner, options.Repo, missingLabels, cts)
            .ConfigureAwait(false);
        existingLabels.AddRange(createdLabels);
        return labels;
    }

    private GithubState GetGithubState(JiraIssue jiraIssue)
    {
        var state = GithubState.Open;
        var stateKey = _settings.Jira.StateMapping.Any(kvp => kvp.Value.Contains(jiraIssue.Fields.Status.Name, StringComparer.OrdinalIgnoreCase));
        if (!stateKey)
        {
            _logger.LogError("Could not find {State} in state mapping", jiraIssue.Fields.Status.Name);
        }
        else
        {
            state = _settings.Jira.StateMapping.FirstOrDefault(kvp => kvp.Value.Contains(jiraIssue.Fields.Status.Name)).Key;
        }

        return state;
    }

    private async Task<int?> GetMilestoneNumber(JiraIssue jiraIssue, JihubOptions options, List<GitHubMilestone> milestones, CancellationToken cts)
    {
        int? milestoneNumber = null;
        if (!jiraIssue.Fields.FixVersions.Any())
        {
            return milestoneNumber;
        }

        var fixVersion = jiraIssue.Fields.FixVersions.Last();
        var milestone = milestones.SingleOrDefault(x => x.Title.Equals(fixVersion.Name, StringComparison.OrdinalIgnoreCase));
        if (milestone != null)
        {
            return milestone.Number;
        }

        milestone = await _githubService
            .CreateMilestoneAsync(fixVersion.Name, options.Owner, options.Repo, cts)
            .ConfigureAwait(false);
        milestones.Add(milestone);

        return milestone.Number;
    }

    private static string ReplaceMatch(Match match, ICollection<(string JiraDescriptionUrl, GithubAsset Asset)> linkedAttachments, bool authorizedLink)
    {
        var url = match.Groups[1].Value;
        var matchingAttachment = linkedAttachments.SingleOrDefault(x => x.JiraDescriptionUrl == match.Groups[0].Value);
        var linkAsContent = authorizedLink ? string.Empty : "!";
        var result = matchingAttachment == default ?
            $"{linkAsContent}[{url.Split("/").LastOrDefault()}]({url})" :
            $"{linkAsContent}[{matchingAttachment.Asset.Name}]({matchingAttachment.Asset.Url})";
        linkedAttachments.Remove(matchingAttachment);
        return result;
    }

    private static string ReplaceLinks(Match match)
    {
        var link = match.Groups[0].Value
            .Replace("[", string.Empty)
            .Replace("]", string.Empty);
        if (link.Contains("|"))
        {
            var linkElements = link.Split("|");
            return $"[{linkElements[0]}]({linkElements[1]})";
        }

        return $"[{link.Split("/").Last()}]({link})";
    }
}
