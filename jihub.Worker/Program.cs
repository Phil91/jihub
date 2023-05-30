/********************************************************************************
 * Copyright (c) 2021, 2023 BMW Group AG
 * Copyright (c) 2021, 2023 Contributors to the Eclipse Foundation
 *
 * See the NOTICE file(s) distributed with this work for additional
 * information regarding copyright ownership.
 *
 * This program and the accompanying materials are made available under the
 * terms of the Apache License, Version 2.0 which is available at
 * https://www.apache.org/licenses/LICENSE-2.0.
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations
 * under the License.
 *
 * SPDX-License-Identifier: Apache-2.0
 ********************************************************************************/

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
    Console.WriteLine("Building worker");
    var host = Host
        .CreateDefaultBuilder(args)
        .ConfigureServices((hostContext, services) =>
        {
            services
                .AddTransient<Worker>()
                .AddJiraService(hostContext.Configuration.GetSection("Jira"))
                .AddGithubService(hostContext.Configuration.GetSection("Github"))
                .AddParsers(hostContext.Configuration.GetSection("Parsers"))
                .AddLogging();
        })
        .Build();
    Console.WriteLine("Building importer completed");

    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (s, e) =>
    {
        Console.WriteLine("Canceling...");
        cts.Cancel();
        e.Cancel = true;
    };

    Console.WriteLine("Starting...");
    var workerInstance = host.Services.GetRequiredService<Worker>();
    await Parser.Default.ParseArguments<JihubOptions>(args)
        .MapResult(async opts =>
            {
                opts.Validate();

                try
                {
                    // We have the parsed arguments, so let's just pass them down
                    return await workerInstance.ExecuteAsync(opts, cts.Token);
                }
                catch
                {
                    Console.WriteLine("Error!");
                    return -3; // Unhandled error
                }
            },
            _ => Task.FromResult(-1)); 
    Console.WriteLine("Execution finished shutting down");
}
catch (Exception ex)
{
    Console.WriteLine(ex.ToString());
}
