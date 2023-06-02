using System.ComponentModel.DataAnnotations;

namespace jihub.Github.DependencyInjection;

/// <summary>
/// Settings used for github.
/// </summary>
public class GithubServiceSettings
{
    [Required(AllowEmptyStrings = false)]
    public string Token { get; set; } = null!;
}
