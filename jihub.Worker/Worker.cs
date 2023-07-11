using jihub.Base;
using jihub.Github.Models;
using jihub.Github.Services;
using jihub.Jira;
using jihub.Parsers.Jira;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace jihub;

/// <summary>
/// Worker to process the jql query.
/// </summary>
public class Worker
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<Worker> _logger;

    /// <summary>
    /// Creates a new instance of <see cref="Worker"/>
    /// </summary>
    /// <param name="serviceScopeFactory">access to the services</param>
    /// <param name="logger">the logger</param>
    public Worker(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<Worker> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Handles the conversion from jira to github issues and the import to github
    /// </summary>
    /// <param name="options">the options given by the user</param>
    /// <param name="cts">Cancellation Token</param>
    public async Task<int> ExecuteAsync(JihubOptions options, CancellationToken cts)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var jiraService = scope.ServiceProvider.GetRequiredService<IJiraService>();
            var githubService = scope.ServiceProvider.GetRequiredService<IGithubService>();
            var parser = scope.ServiceProvider.GetRequiredService<IJiraParser>();

            var jiraIssues = await jiraService.GetAsync(options.SearchQuery, options.MaxResults, cts).ConfigureAwait(false);
            var content = Enumerable.Empty<GithubContent>();
            if (options.Export)
            {
                content = await githubService.GetRepoContent(options.ImportOwner!, options.UploadRepo!, "contents", cts).ConfigureAwait(false);
            }

            var githubInformation = await githubService.GetRepositoryData(options.Owner, options.Repo, cts).ConfigureAwait(false);

            var excludedJiraIssues = jiraIssues.Where(x => githubInformation.Issues.Any(i => i.Title.Contains($"(ext: {x.Key})")));
            _logger.LogInformation("The following issues were not imported because they are already available in Github {Issues}", string.Join(",", excludedJiraIssues.Select(x => x.Key)));

            var convertedIssues = await parser.ConvertIssues(jiraIssues.Except(excludedJiraIssues), options, content, githubInformation.Labels.ToList(), githubInformation.Milestones.ToList(), cts).ConfigureAwait(false);
            await githubService.CreateIssuesAsync(options.Owner, options.Repo, convertedIssues, cts).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Environment.ExitCode = 1;
            _logger.LogError(ex, "processing failed with following Exception {ExceptionMessage}", ex.Message);
        }

        return 0;
    }
}
