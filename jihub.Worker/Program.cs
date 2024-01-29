using CommandLine;
using jihub;
using jihub.Base;
using jihub.Github.DependencyInjection;
using jihub.Jira.DependencyInjection;
using jihub.Parsers.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

try
{
    var options = Parser.Default.ParseArguments<JihubOptions>(args)
        .MapResult(opts =>
            {
                opts.Validate();
                return opts;
            },
            err =>
            {
                foreach (var error in err)
                    Console.WriteLine(error.Tag.ToString());

                throw new ArgumentException("failed to parse options");
            });

    Console.WriteLine("Building worker");
    await Host
        .CreateDefaultBuilder(args)
        .ConfigureServices((hostContext, services) =>
        {
            Console.WriteLine("Building services");
            services
                .AddJiraService(hostContext.Configuration.GetSection("Jira"))
                .AddGithubService(hostContext.Configuration.GetSection("Github"))
                .AddParsers(hostContext.Configuration.GetSection("Parsers"))
                .AddLogging();

                services.AddSingleton(options);
                services
                    .AddHostedService<Worker>();
        })
        .RunConsoleAsync();

    Console.WriteLine("Execution finished shutting down");
}
catch (Exception ex)
{
    Console.WriteLine(ex.ToString());
}
