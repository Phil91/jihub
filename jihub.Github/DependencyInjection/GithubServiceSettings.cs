using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;

namespace jihub.Github.DependencyInjection;

/// <summary>
/// Settings used for github.
/// </summary>
public class GithubServiceSettings
{
    [Required(AllowEmptyStrings = false)]
    public string Token { get; set; } = null!;
}
