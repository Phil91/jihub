namespace jihub.Github.Models;

public record GithubUser(
    string Name);

public record GithubUserEmail(
    string Email,
    bool Primary);
