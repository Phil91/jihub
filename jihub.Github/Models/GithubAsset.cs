using System.Text.Json.Serialization;

namespace jihub.Github.Models;

public record GithubAssetContent(GithubAsset Content);

public record GithubAsset(
    [property: JsonPropertyName("html_url")] string Url,
    string Name);
