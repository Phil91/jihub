using CommandLine;

namespace jihub
{
    /// <summary>
    /// Command line options
    /// </summary>
    public class CommandLineOptions
    {
        /// <summary>
        /// Name of the github project
        /// </summary>
        [Option(shortName: 'r', longName: "repo", Required = true, HelpText = "Github Repository")]
        public string Repo { get; set; } = null!;

        /// <summary>
        /// Username of the github user / organisation that hosts the project
        /// </summary>
        [Option(shortName: 'o', longName: "owner", Required = true, HelpText = "Github Repository Owner (User or Organisation)")]
        public string Owner { get; set; } = null!;

        /// <summary>
        /// The max results when requesting jira
        /// </summary>
        [Option(shortName: 'm', longName: "max-results", Required = false, HelpText = "The max jira results", Default = 1000)]
        public int MaxResults { get; set; } = 1000;
        
        /// <summary>
        /// The search query to get only the needed jira tickets
        /// </summary>
        [Option(shortName: 'q', longName: "query", Required = true, HelpText = "Search query to filter jira issues")]
        public string SearchQuery { get; set; } = null!;
    }
}