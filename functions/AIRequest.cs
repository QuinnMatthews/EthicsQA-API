using DarkLoop.Azure.Functions.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Net;

namespace EthicsQA.API
{
    [FunctionAuthorize]
    public class AIRequest
    {
        private readonly ILogger<AIRequest> _logger;
        private readonly IOpenAIService _openAIService;
        private readonly Configuration _aiconfiguration;

        public AIRequest(
            ILogger<AIRequest> logger,
            IOpenAIService openAIService,
            IOptions<Configuration> options
        )
        {
            _logger = logger;
            _openAIService = openAIService;
            _aiconfiguration = options.Value;
        }

        [Function("AIRequest")]
        public async Task<IActionResult> Run([HttpTrigger("get")] HttpRequest req)
        {
            _logger.LogInformation("Processing AI request.");

            //TODO Handle User Authentication/authorization

            // Validate stance parameter
            if (!req.Query.ContainsKey("stance"))
            {
                return new BadRequestObjectResult("Please pass a stance on the query string");
            }

            if (!Enum.TryParse<Stance>(req.Query["stance"], true, out var stance))
            {
                return new BadRequestObjectResult(
                    "Invalid stance. Please pass a valid stance on the query string"
                );
            }

            // Validate scenario parameter
            if (!req.Query.ContainsKey("scenario"))
            {
                return new BadRequestObjectResult("Please pass a scenario on the query string");
            }

            string scenario = req.Query["scenario"]!;

            // Generate messages
            string stanceMsg = stance switch
            {
                Stance.Pro
                    => "You will respond only with ethical implications that are in favor of the given scenario.",
                Stance.Con
                    => "You will respond only with ethical implications that are against the given scenario.",
                Stance.Neutral
                    => "You will respond with ethical implications that are both in favor and against the given scenario.",
                _ => throw new NotImplementedException()
            };

            var messages = new List<Message>
            {
                Message.Create(
                    ChatRoleType.System,
                    "You are a helpful assistant that evaluates the ethical implications of a given scenario. "
                        + stanceMsg
                ),
                Message.Create(ChatRoleType.User, scenario),
            };

            // Get AI response
            var AIResp = await _openAIService.Chat.Get(
                messages,
                o =>
                {
                    o.MaxTokens = 150;
                    o.Model = "gpt-4";
                }
            );

            return new OkObjectResult(AIResp);
        }

        enum Stance
        {
            Pro,
            Con,
            Neutral
        }
    }
}
