using jihub.Parsers.Jira;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace jihub.Parsers.DependencyInjection;

public static class ParserServiceCollectionExtension
{
    public static IServiceCollection AddParsers(this IServiceCollection services, IConfigurationSection section)
    {
        services.AddOptions<ParserSettings>()
            .Bind(section)
            .ValidateOnStart();

        services
            .AddTransient<IJiraParser, JiraParser>();

        return services;
    }
}
