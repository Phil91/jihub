using jihub.Github.Models;

namespace jihub.Parsers.DependencyInjection;

public class JiraParserOptions
{
    public IDictionary<GithubState, IEnumerable<string>> StateMapping { get; set; } = null!;
}
