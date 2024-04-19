using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using PhoneNumbers;

namespace EthicsQA.API
{
    public class ExchangeCode
    {
        private readonly ILogger<AIRequest> _logger;
        private readonly Configuration _configuration;

        public ExchangeCode(ILogger<AIRequest> logger, IOptions<Configuration> options)
        {
            _logger = logger;
            _configuration = options.Value;
        }

        /*
        * Function to exchange a 6-digit SMS code for an access token
        *
        * Request body:
        * {
        *     "Phone": "+15555555555",
        *     "Code": "123456"
        * }
        *
        * Response:
        * {
        *     "access_token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJtb2JpbGVQaG9uZSI6IisxNTU1NTU1NTU1NTU1NSJ9.8
        * }
        */

        [Function("ExchangeCode")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req
        )
        {
            //
            // Validate request
            //

            // Validate code parameter
            if (!(req.ContentType == "application/json"))
            {
                return new BadRequestObjectResult("Request must be in JSON format");
            }

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            if (string.IsNullOrEmpty(requestBody))
            {
                return new BadRequestObjectResult("Please pass a code in the request body");
            }

            RequestData requestData;

            try
            {
                requestData =
                    JsonConvert.DeserializeObject<RequestData>(
                        requestBody,
                        new JsonSerializerSettings
                        {
                            MissingMemberHandling = MissingMemberHandling.Error,
                            EqualityComparer = StringComparer.OrdinalIgnoreCase,
                        }
                    ) ?? throw new JsonSerializationException();
            }
            catch
            {
                return new BadRequestObjectResult("Error parsing request body.");
            }

            string code = requestData.Code;
            string phone = requestData.Phone;

            // Convert phone number to E.164 format
            var phoneUtil = PhoneNumberUtil.GetInstance();
            try
            {
                var phoneNumber = phoneUtil.Parse(phone, "US");
                if (!phoneUtil.IsValidNumber(phoneNumber))
                {
                    return new BadRequestObjectResult(
                        "Invalid phone number. Please pass a valid phone number in the request body"
                    );
                }
                phone = phoneUtil.Format(phoneNumber, PhoneNumberFormat.E164);
            }
            catch (NumberParseException)
            {
                return new BadRequestObjectResult(
                    "Invalid phone number. Please pass a valid phone number in the request body"
                );
            }

            //
            // Validate code
            //

            // Create CosmosClient
            using CosmosClient client = new CosmosClient(_configuration.DB_Connection_String);
            var container = client.GetContainer(
                _configuration.DB_Name,
                _configuration.DB_User_Container
            );

            // Check if user exists in database or create new user if not
            User? user = container
                .GetItemLinqQueryable<User>(true)
                .Where(u => u.Phone == phone)
                .ToList()
                .FirstOrDefault();

            if (user == null)
            {
                return new ObjectResult("Invalid Code")
                {
                    StatusCode = StatusCodes.Status401Unauthorized
                };
            }

            // Check if code is correct
            if (user.SMSCode != code)
            {
                return new ObjectResult("Invalid Code")
                {
                    StatusCode = StatusCodes.Status401Unauthorized
                };
            }

            // Check if code is expired
            if (
                user.SMSCodeTimestamp == null
                || user.SMSCodeTimestamp < DateTime.UtcNow.AddMinutes(-10)
            )
            {
                return new ObjectResult("Code Expired")
                {
                    StatusCode = StatusCodes.Status401Unauthorized
                };
            }

            // Check if code has already been used
            if (user.SMSCodeUsed)
            {
                return new ObjectResult("Code Already Used")
                {
                    StatusCode = StatusCodes.Status401Unauthorized
                };
            }

            //
            // Code is correct and not expired
            //

            // Mark code as used
            user.SMSCodeUsed = true;

            // Update user in database
            try
            {
                await container.UpsertItemAsync(user);
            }
            catch
            {
                _logger.LogError("Failed to update user in database");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }

            // Generate JWT
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration.JWT_Secret);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                    new Claim(ClaimTypes.MobilePhone, phone)
                }),
                Expires = DateTime.UtcNow.AddHours(2),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature
                )
            };
            var access_token = tokenHandler.CreateToken(tokenDescriptor);

            return new OkObjectResult(new { access_token = tokenHandler.WriteToken(access_token) });
        }

        public class RequestData
        {
            public required string Phone { get; set; }
            public required string Code { get; set; }
        }
    }
}
