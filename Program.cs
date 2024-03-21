using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using EthicsQA.API;
using OpenAI.Net;

var builder = new HostBuilder();

builder.ConfigureFunctionsWebApplication();


builder.ConfigureServices(services => {
    services.AddApplicationInsightsTelemetryWorkerService();
    services.ConfigureFunctionsApplicationInsights();
    services.AddOptions<AIConfiguration>()
            .Configure<IConfiguration>((settings, configuration) =>
            {
                configuration.Bind("AI", settings);
            });
    services.AddOpenAIServices(options => {

            options.ApiKey = services.BuildServiceProvider().GetService<IOptions<AIConfiguration>>()!.Value.OpenAI_API_KEY;
    });
});


var host = builder.Build();
host.Run();
