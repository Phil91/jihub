using System.Net.Http.Headers;
using jihub.Github.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace jihub.Github.DependencyInjection;

public static class GithubServiceCollectionExtension
{
    public static IServiceCollection AddGithubService(this IServiceCollection services, IConfigurationSection section)
    {
        services.AddOptions<GithubServiceSettings>()
            .Bind(section)
            .ValidateOnStart();

        var sp = services.BuildServiceProvider();
        var settings = sp.GetRequiredService<IOptions<GithubServiceSettings>>().Value;
        
        services.AddHttpClient(nameof(GithubService), c =>
        {
            c.BaseAddress = new Uri("https://api.github.com/");
            c.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            c.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("jihub", "0.1"));
            c.DefaultRequestHeaders.Connection.Add("keep-alive");

            c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.Token);
        });

        services
            .AddTransient<IGithubService, GithubService>()
            .AddTransient<IGithubParser, GithubParser>();

        return services;
    }
}
