using CommandLine;

namespace jihub
{
    public class CommandLineOptions
    {
        /// Name of the github project
        [Option(shortName: 'r', longName: "repo", Required = true, HelpText = "Github Repository")]
        public string Repo { get; set; } = null!;

        /// Username of the github user / organisation that hosts the project
        [Option(shortName: 'o', longName: "owner", Required = true, HelpText = "Github Repository Owner (User or Organisation)")]
        public string Owner { get; set; } = null!;

        /// The max results when requesting jira
        [Option(shortName: 'm', longName: "max-results", Required = false, HelpText = "The max jira results", Default = 1000)]
        public int MaxResults { get; set; } = 1000;

        [Option(shortName: 'q', longName: "query", Required = true, HelpText = "Search query to filter jira issues")]
        public string SearchQuery { get; set; } = null!;
    }
}