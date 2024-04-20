using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using EthicsQA.API;
using OpenAI.Net;
using Microsoft.Extensions.Logging;

var builder = new HostBuilder();

builder.ConfigureFunctionsWebApplication(builder => {
    builder.UseFunctionsAuthorization();
}).ConfigureServices(services => {
    services.AddApplicationInsightsTelemetryWorkerService();
    services.ConfigureFunctionsApplicationInsights();
    services.AddOptions<Configuration>()
            .Configure<IConfiguration>((settings, configuration) =>
                configuration.Bind(settings)
            );
    services.AddOpenAIServices(options => {
            options.ApiKey = services.BuildServiceProvider().GetService<IOptions<Configuration>>()!.Value.OpenAI_API_KEY;
    });
    
    services.AddFunctionsAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(services.BuildServiceProvider().GetService<IOptions<Configuration>>()!.Value.JWT_Secret))
        };

        options.Events = new JwtBearerEvents
        {
            // Log any authentication failures
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerHandler>>();
                logger.LogError(context.Exception, "Authentication failed.");
                return Task.CompletedTask;
            }
        };
    });
});

var host = builder.Build();
host.Run();
