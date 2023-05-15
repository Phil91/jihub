using System.ComponentModel.DataAnnotations;

namespace jihub.Jira.DependencyInjection;

/// <summary>
/// Settings used in business logic concerning connectors.
/// </summary>
public class JiraServiceSettings
{
    [Required(AllowEmptyStrings = false)]
    public string JiraInstanceUrl { get; set; } = null!;

    [Required(AllowEmptyStrings = false)]
    public string JiraUser { get; set; } = null!;
    
    [Required(AllowEmptyStrings = false)]
    public string JiraPassword { get; set; } = null!;
}