namespace jihub.Github.Models;

/// <summary>
/// Model to upload a file to github
/// </summary>
/// <param name="Message"></param>
/// <param name="Content"></param>
public record UploadFileContent(
    string Message,
    // Committer Committer,
    // string Sha,
    string Content,
    string Branch
);

/// <summary>
/// Committer data for the file upload
/// </summary>
/// <param name="Name">Name of the github user</param>
/// <param name="Email">Email of the github user</param>
public record Committer(
    string Name,
    string Email
);
