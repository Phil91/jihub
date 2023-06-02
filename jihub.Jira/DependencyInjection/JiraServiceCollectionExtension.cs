using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace jihub.Jira.DependencyInjection;

public static class JiraServiceCollectionExtension
{
    public static IServiceCollection AddJiraService(this IServiceCollection services, IConfigurationSection section)
    {
        services.AddOptions<JiraServiceSettings>()
            .Bind(section)
            .ValidateOnStart();

        var sp = services.BuildServiceProvider();
        var settings = sp.GetRequiredService<IOptions<JiraServiceSettings>>().Value;

        var authenticationString = $"{settings.JiraUser}:{settings.JiraPassword}";
        var auth = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(authenticationString));
        var baseUrl = settings.JiraInstanceUrl;
        baseUrl = baseUrl.EndsWith("/") ? baseUrl : $"{baseUrl}/";
        services.AddHttpClient(nameof(JiraService), c =>
        {
            c.BaseAddress = new Uri($"{baseUrl}rest/api/2/search");
            c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
        });
        services.AddHttpClient($"{nameof(JiraService)}Download", c =>
        {
            c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
        });

        services
            .AddTransient<IJiraService, JiraService>();

        return services;
    }
}
