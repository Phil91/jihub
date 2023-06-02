using System.Text.Json.Serialization;

namespace jihub.Github.Models;

public enum GithubState
{
    [JsonPropertyName("open")]
    Open,

    [JsonPropertyName("closed")]
    Closed
}
