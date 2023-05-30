using CommandLine;

namespace jihub.Base
{
    /// <summary>
    /// Command line options
    /// </summary>
    public class JihubOptions
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
        
        /// <summary>
        /// The search query to get only the needed jira tickets
        /// </summary>
        [Option(shortName: 'l', longName: "link", Required = false, HelpText = "If set all external resources such as images will be refered as a link in the description", Default = false)]
        public bool Link { get; set; }

        /// <summary>
        /// The search query to get only the needed jira tickets
        /// </summary>
        [Option(shortName: 'c', longName: "content-link", Required = false, HelpText = "If set all external resources such as images will be linked as content in the description", Default = false)]
        public bool ContentLink { get; set; }

        /// <summary>
        /// The search query to get only the needed jira tickets
        /// </summary>
        [Option(shortName: 'e', longName: "export", Required = false, HelpText = "If set all external resources such as images will be exported to the given repository", Default = false)]
        public bool Export { get; set; }

        /// <summary>
        /// The repository where the assets of jira gets uploaded
        /// </summary>
        [Option(shortName: 'u', longName: "upload-repo", Required = false, HelpText = "Upload repository for the jira assets.")]
        public string? UploadRepo { get; set; } = null;

        /// <summary>
        /// The repository where the assets of jira gets uploaded
        /// </summary>
        [Option(shortName: 'i', longName: "import-owner", Required = false, HelpText = "Owner of the repository the assets should be uploaded to.")]
        public string? ImportOwner { get; set; } = null;

        /// <summary>
        /// Checks the options if everything is correct
        /// </summary>
        /// <exception cref="ConfigurationException">Exception if the configuration is incorrect</exception>
        public void Validate()
        {
            if (!Link && !ContentLink)
                throw new ConfigurationException($"Please choose one of the following options: {nameof(Link)}, {nameof(ContentLink)}");

            if (MaxResults > 1000)
                throw new ConfigurationException($"{nameof(MaxResults)} must not exceed 1000");

            if (Export && (string.IsNullOrWhiteSpace(UploadRepo) || string.IsNullOrWhiteSpace(ImportOwner)))
            {
                throw new ConfigurationException("Upload repo and import owner must be set if the assets should be imported");
            }
        }
    }
}
