using System.ComponentModel.DataAnnotations;

namespace jihub.Jira.DependencyInjection;

/// <summary>
/// Settings used for jira
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