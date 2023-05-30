using System.Net.Http.Headers;
using jihub.Github.Services;
using jihub.Parsers;
using jihub.Parsers.Jira;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace jihub.Parsers.DependencyInjection;

public static class ParserServiceCollectionExtension
{
    public static IServiceCollection AddParsers(this IServiceCollection services, IConfigurationSection section)
    {
        services
            .AddTransient<IJiraParser, JiraParser>();

        return services;
    }
}
