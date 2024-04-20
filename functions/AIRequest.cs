using DarkLoop.Azure.Functions.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
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
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
        {
            _logger.LogInformation("Processing AI request.");

            // Validate phone number parameter from request post data
            if (!(req.ContentType == "application/json"))
            {
                return new BadRequestObjectResult("Request must be in JSON format");
            }

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            if (string.IsNullOrEmpty(requestBody))
            {
                return new BadRequestObjectResult("Please pass a question in the request body");
            }

            RequestData requestData;

            try {
                requestData = JsonConvert.DeserializeObject<RequestData>(requestBody,
                new JsonSerializerSettings { 
                    MissingMemberHandling = MissingMemberHandling.Error,
                    EqualityComparer = StringComparer.OrdinalIgnoreCase,
                }) ?? throw new JsonSerializationException();
            } catch {
                return new BadRequestObjectResult("Error parsing request body.");
            }

            string question = requestData.Question;
            string _stance = requestData.Stance;


            // Validate stance parameter
            if (!Enum.TryParse<Stance>(_stance, true, out var stance))
            {
                return new BadRequestObjectResult(
                    "Invalid stance. Please pass a valid stance on the query string"
                );
            }

            // Generate messages
            string stanceMsg = stance switch
            {
                Stance.Pro
                    => "You will respond only with ethical implications that are in favor of the given question or scenario.",
                Stance.Con
                    => "You will respond only with ethical implications that are against the given question or scenario.",
                Stance.Neutral
                    => "You will respond with ethical implications that are both in favor and against the given question or scenario.",
                _ => throw new NotImplementedException()
            };

            var messages = new List<Message>
            {
                Message.Create(
                    ChatRoleType.System,
                    "You are a helpful assistant that evaluates the ethical issues surrounding a given question or scenario. "
                        + stanceMsg
                ),
                Message.Create(ChatRoleType.User, question),
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

        class RequestData
        {
            public required string Question { get; set; }
            public required string Stance { get; set; }
        }

        enum Stance
        {
            Pro,
            Con,
            Neutral
        }
    }
}
