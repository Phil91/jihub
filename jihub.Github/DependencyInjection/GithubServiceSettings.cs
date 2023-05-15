using System.ComponentModel.DataAnnotations;

namespace jihub.Github.DependencyInjection;

/// <summary>
/// Settings used in business logic concerning connectors.
/// </summary>
public class GithubServiceSettings
{
    [Required(AllowEmptyStrings = false)]
    public string Token { get; set; } = null!;
}