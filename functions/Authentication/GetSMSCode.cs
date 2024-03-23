using Azure.Communication.Sms;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using PhoneNumbers;
using System.Security.Cryptography;

namespace EthicsQA.API
{
    public class GetSMSCode
    {
        private readonly ILogger<GetSMSCode> _logger;
        private readonly Configuration _configuration;

        public GetSMSCode(ILogger<GetSMSCode> logger, IOptions<Configuration> configuration)
        {
            _logger = logger;
            _configuration = configuration.Value;
        }

        [Function("GetSMSCode")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
        {
            //
            // Validate request
            //

            // Validate phone number parameter from request post data
            if (!(req.ContentType == "application/json"))
            {
                return new BadRequestObjectResult("Request must be in JSON format");
            }

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            if (string.IsNullOrEmpty(requestBody))
            {
                return new BadRequestObjectResult("Please pass a phone number in the request body");
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

            string phone = requestData.Phone;

            // Convert phone number to E.164 format
            var phoneUtil = PhoneNumberUtil.GetInstance();
            try
            {
                var phoneNumber = phoneUtil.Parse(phone, "US");
                if (!phoneUtil.IsValidNumber(phoneNumber))
                {
                    return new BadRequestObjectResult("Invalid phone number. Please pass a valid phone number in the request body");
                }
                phone = phoneUtil.Format(phoneNumber, PhoneNumberFormat.E164);
            }
            catch (NumberParseException)
            {
                return new BadRequestObjectResult("Invalid phone number. Please pass a valid phone number in the request body");
            }

            //
            // Generate secure random 6-digit SMS code
            //
            const int codeLength = 6;
            int[] code = new int[codeLength];

            for(int i = 0; i < codeLength; i++)
            {
                code[i] += RandomNumberGenerator.GetInt32(0, 9);
            }

            //
            // Store code with timestamp in database
            //

            // Create CosmosClient
            using CosmosClient client = new CosmosClient(_configuration.DB_Connection_String);
            var container = client.GetContainer(_configuration.DB_Name, _configuration.DB_User_Container);

            // Check if user exists in database or create new user if not
            User user = container.GetItemLinqQueryable<User>(true)
                .Where(u => u.Phone == phone)
                .ToList()
                .FirstOrDefault()
                ?? new User
                    {
                        id = Guid.NewGuid(),
                        Phone = phone,
                    };
            
            // Check cooldown on SMS code generation
            if (user.SMSCodeTimestamp != null && DateTime.UtcNow.Subtract(user.SMSCodeTimestamp.Value).TotalMinutes < 1)
            {
                return new BadRequestObjectResult("Please wait at least 1 minute before requesting a new SMS code");
            }

            user.SMSCode = string.Join("", code);
            user.SMSCodeTimestamp = DateTime.UtcNow;
            user.SMSCodeUsed = false;

            // Upsert user in database
            try {
                await container.UpsertItemAsync(user);
            } catch(CosmosException e) {
                _logger.LogError("Failed to upsert user in database: " + e.Message);
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }

            //
            // Send SMS with code to phone number
            //
            SmsClient smsClient = new SmsClient(_configuration.Communication_Services_Connection_String);
            try
            {
                await smsClient.SendAsync(
                    from:_configuration.Communication_Services_Phone,
                    to: phone,
                    message: "Your verification code is: " + user.SMSCode
                );
            }
            catch
            {
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }

            //
            // Return success message
            //
            return new OkObjectResult("SMS code sent to " + phone);
        }

        private class RequestData
        {
            public required string Phone { get; set; }
        }
    }
}
