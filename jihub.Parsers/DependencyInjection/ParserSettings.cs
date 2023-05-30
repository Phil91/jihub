namespace jihub.Parsers.DependencyInjection;

/// <summary>
/// Settings used in business logic concerning connectors.
/// </summary>
public class ParserSettings
{
    public JiraParserOptions Jira { get; set; } = null!;
}