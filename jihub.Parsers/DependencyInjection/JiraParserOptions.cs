using System.ComponentModel.DataAnnotations;
using jihub.Github.Models;

namespace jihub.Parsers.DependencyInjection;

public class JiraParserOptions
{
    [Required]
    public IDictionary<GithubState, IEnumerable<string>> StateMapping { get; set; } = new Dictionary<GithubState, IEnumerable<string>>();

    [Required]
    public IEnumerable<EmailMapping> EmailMappings { get; set; } = new List<EmailMapping>();

    [Required]
    public string DescriptionTemplate { get; set; } = null!;
}

public record EmailMapping(string JiraMail, string GithubName);
