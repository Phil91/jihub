using jihub.Base;
using jihub.Github.Models;
using jihub.Github.Services;
using jihub.Jira;
using jihub.Jira.Models;
using jihub.Parsers.Jira;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace jihub;

/// <summary>
/// Worker to process the jql query.
/// </summary>
public class Worker : IHostedService
{
    private readonly IJiraService _jiraService;
    private readonly IGithubService _githubService;
    private readonly IJiraParser _jiraParser;
    private readonly JihubOptions _jihubOptions;
    private readonly ILogger<Worker> _logger;

    /// <summary>
    /// Creates a new instance of <see cref="Worker"/>
    /// </summary>
    /// <param name="jiraParser"></param>
    /// <param name="jihubOptions"></param>
    /// <param name="logger">the logger</param>
    /// <param name="jiraService"></param>
    /// <param name="githubService"></param>
    public Worker(
        IJiraService jiraService,
        IGithubService githubService,
        IJiraParser jiraParser,
        JihubOptions jihubOptions,
        ILogger<Worker> logger)
    {
        _jiraService = jiraService;
        _githubService = githubService;
        _jiraParser = jiraParser;
        _jihubOptions = jihubOptions;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cts)
    {
        try
        {
            var jiraIssues = await _jiraService.GetAsync(_jihubOptions.SearchQuery, _jihubOptions.MaxResults, cts).ConfigureAwait(false);
            var content = Enumerable.Empty<GithubContent>();
            if (_jihubOptions.Export)
            {
                content = await _githubService.GetRepoContent(_jihubOptions.ImportOwner!, _jihubOptions.UploadRepo!, "contents", cts).ConfigureAwait(false);
            }

            var githubInformation = await _githubService.GetRepositoryData(_jihubOptions.Owner, _jihubOptions.Repo, cts).ConfigureAwait(false);

            var excludedJiraIssues = jiraIssues.Where(x => githubInformation.Issues.Any(i => i.Title.Contains($"(ext: {x.Key})")));
            _logger.LogInformation("The following issues were not imported because they are already available in Github {Issues}", string.Join(",", excludedJiraIssues.Select(x => x.Key)));

            var convertedIssues = await _jiraParser.ConvertIssues(jiraIssues.Except(excludedJiraIssues), _jihubOptions, content, githubInformation.Labels.ToList(), githubInformation.Milestones.ToList(), cts).ConfigureAwait(false);
            var createdIssues = await _githubService.CreateIssuesAsync(_jihubOptions.Owner, _jihubOptions.Repo, convertedIssues, cts).ConfigureAwait(false);

            if (_jihubOptions.LinkChildren)
            {
                var childIssues = GetLinks(jiraIssues, "Parent of");
                await _githubService.LinkChildren(_jihubOptions.Owner, _jihubOptions.Repo, childIssues, githubInformation.Issues, createdIssues, cts).ConfigureAwait(false);
            }

            if (_jihubOptions.LinkRelated)
            {
                var relatedIssues = GetLinks(jiraIssues, "relates to");
                await _githubService.AddRelatesComment(_jihubOptions.Owner, _jihubOptions.Repo, relatedIssues, githubInformation.Issues, createdIssues, cts).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Environment.ExitCode = 1;
            _logger.LogError(ex, "processing failed with following Exception {ExceptionMessage}", ex.Message);
        }
    }

    private static Dictionary<string, List<string>> GetLinks(IEnumerable<JiraIssue> jiraIssues, string linkType)
    {
        var dict = new Dictionary<string, List<string>>();
        foreach (var (key, issueFields) in jiraIssues.Where(x => x.Fields.IssueLinks != null))
        {
            var links = issueFields.IssueLinks!
                .Where(link =>
                    (link.Type.Inward.Equals(linkType, StringComparison.OrdinalIgnoreCase) && link.InwardIssue != null) ||
                    (link.Type.Outward.Equals(linkType, StringComparison.OrdinalIgnoreCase) && link.OutwardIssue != null)
                )
                .Select(link => link.InwardIssue ?? link.OutwardIssue)
                .Select(x => x!.Key)
                .ToList();
            if (!dict.ContainsKey(key))
            {
                dict.Add(key, links);
            }
            else
            {
                dict[key].AddRange(links);
            }
        }

        return dict;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
