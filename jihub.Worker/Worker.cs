using jihub.Base;
using jihub.Github;
using jihub.Github.Services;
using jihub.Jira;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace jihub;

/// <summary>
/// Service that reads all open/pending processSteps of a checklist and triggers their execution.
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
    /// Handles the checklist processing
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
            var parser = scope.ServiceProvider.GetRequiredService<IGithubParser>();

            var jiraIssues = await jiraService.GetAsync(options.SearchQuery, options.MaxResults, cts).ConfigureAwait(false);
            var githubInformation = await githubService.GetMilestonesAndLabelsAsync(options.Owner, options.Repo, cts).ConfigureAwait(false);
            var convertedIssues = await parser.ConvertIssues(jiraIssues, options, githubInformation.Labels.ToList(), githubInformation.Milestones.ToList(), cts).ConfigureAwait(false);
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
