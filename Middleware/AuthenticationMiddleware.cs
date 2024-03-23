// using Microsoft.Azure.Functions.Worker;
// using Microsoft.Azure.Functions.Worker.Middleware;
// using Microsoft.Extensions.Options;
// using System.Reflection;
// using Microsoft.IdentityModel.Tokens;
// using System.Text.Json;
// using System.Text;
// using Microsoft.Azure.Functions.Worker.Http;
// using System.IdentityModel.Tokens.Jwt;

// namespace EthicsQA.API;

// public class AuthenticationMiddleware(IOptions<Configuration> configuration) : IFunctionsWorkerMiddleware
// {
//     private readonly Configuration _configuration = configuration.Value;

//     public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
//     {
//         //
//         // Check if the function requires authorization
//         // 
//         var method = context.GetTargetFunctionMethod();
//         var attributes = method.GetCustomAttributes(typeof(AuthorizeAttribute), true);
//         if (attributes.Length == 0)
//         {
//             // Continue with the next middleware
//             await next(context);
//             return;
//         }
        
//         //
//         // Validate token
//         //
//         // Extract token from headers
//         // if (!TryGetTokenFromHeaders(context, out var token)) {
//         //     var httpReqData = await context.GetHttpRequestDataAsync();
//         //     if (httpReqData != null)
//         //     {
//         //         var newResponse = httpReqData.CreateResponse();
//         //         newResponse.StatusCode = System.Net.HttpStatusCode.Unauthorized;
//         //         await newResponse.WriteStringAsync("Unable to extract token from headers.");

//         //         // Update invocation result with the new http response.
//         //         context.GetInvocationResult().Value = newResponse;

//         //         return;
//         //     }
//         // }

//         // // Validate token
//         // if (!ValidateToken(token!, out var validatedToken))
//         // {
//         //     var httpReqData = await context.GetHttpRequestDataAsync();
//         //     if (httpReqData != null)
//         //     {
//         //         var newResponse = httpReqData.CreateResponse();
//         //         newResponse.StatusCode = System.Net.HttpStatusCode.Unauthorized;
//         //         await newResponse.WriteStringAsync("Invalid token.");

//         //         // Update invocation result with the new http response.
//         //         context.GetInvocationResult().Value = newResponse;

//         //         return;
//         //     }
//         // }

//         var req = await context.GetHttpRequestDataAsync();
//         var  identities = req.Identities;
//         if (!authenticationStatus) return authenticationResponse;
//                 req.HttpContext.VerifyUserHasAnyAcceptedScope(scopeRequiredByApi);


//         //
//         // Continue with the next middleware
//         //
//         await next(context);

//         //
//         // If token close to expiration, refresh token
//         //

//         // // Get token expiration
//         // var exp = validatedToken!.ValidTo;

//         // // If token is close to expiration, refresh token
//         // if (exp < DateTime.UtcNow.AddMinutes(10))
//         // {
//         //     // Refresh token
//         //     var newToken = RefreshToken(token!);

//         //     // Add new token to response headers
//         //     var httpReqData = await context.GetHttpRequestDataAsync();
//         //     if (httpReqData != null)
//         //     {
//         //         var newResponse = httpReqData.CreateResponse();
//         //         newResponse.Headers.Add("Authorization", $"Bearer {newToken}");

//         //         // Update invocation result with the new http response.
//         //         context.GetInvocationResult().Value = newResponse;
//         //     }
//         // }
//     }

//     // Source: https://joonasw.net/view/azure-ad-jwt-authentication-in-net-isolated-process-azure-functions
//     private static bool TryGetTokenFromHeaders(FunctionContext context, out string? token)
//     {
//         token = null;
//         // HTTP headers are in the binding context as a JSON object
//         // The first checks ensure that we have the JSON string
//         if (!context.BindingContext.BindingData.TryGetValue("Headers", out var headersObj))
//         {
//             return false;
//         }

//         if (headersObj is not string headersStr)
//         {
//             return false;
//         }

//         // Deserialize headers from JSON
//         var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(headersStr);
//         if (headers == null)
//         {
//             return false;
//         }

//         var normalizedKeyHeaders = headers.ToDictionary(
//             h => h.Key.ToLowerInvariant(),
//             h => h.Value
//         );

//         if (!normalizedKeyHeaders.TryGetValue("authorization", out var authHeaderValue))
//         {
//             // No Authorization header present
//             return false;
//         }

//         if (!authHeaderValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
//         {
//             // Scheme is not Bearer
//             return false;
//         }

//         token = authHeaderValue.Substring("Bearer ".Length).Trim();
//         return true;
//     }

    

//     private bool ValidateToken(string token, out SecurityToken? validatedToken)
//     {
//         var tokenValidator = new JwtSecurityTokenHandler();
//         var tokenValidationParameters = new TokenValidationParameters
//         {
//             ValidateIssuer = false,
//             ValidateAudience = false,
//             ValidateLifetime = true,
//             ValidateIssuerSigningKey = true,
//             IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(_configuration.JWT_Secret))
//         };

//         try
//         {
//             tokenValidator.ValidateToken(token, tokenValidationParameters, out validatedToken);
//             return true;
//         }
//         catch
//         {
//             validatedToken = null;
//             return false;
//         }
//     }

//     private string RefreshToken(string token)
//     {
//         // Get token expiration
//         var tokenHandler = new JwtSecurityTokenHandler();

//         // Validate token
//         var JWTToken = tokenHandler.ReadToken(token) as JwtSecurityToken;
//         if (JWTToken == null)
//         {
//             throw new SecurityTokenException("Invalid token");
//         }

//         // Create new token
//         var tokenDescriptor = new SecurityTokenDescriptor
//         {
//             Subject = new System.Security.Claims.ClaimsIdentity(JWTToken.Claims),
//             Expires = DateTime.UtcNow.AddHours(2),
//             SigningCredentials = new SigningCredentials(
//                 new SymmetricSecurityKey(Encoding.ASCII.GetBytes(_configuration.JWT_Secret)),
//                 SecurityAlgorithms.HmacSha256Signature
//             )
//         };
//         var newToken = tokenHandler.CreateToken(tokenDescriptor);

//         return tokenHandler.WriteToken(newToken);
//     }
// }

// static class AuthenticationMiddlewareExtensions
// {
//     public static List<T> GetCustomAttributesOnClassAndMethod<T>(MethodInfo targetMethod)
//         where T : Attribute
//     {
//         var methodAttributes = targetMethod.GetCustomAttributes<T>();
//         var classAttributes = targetMethod.DeclaringType.GetCustomAttributes<T>();
//         return methodAttributes.Concat(classAttributes).ToList();
//     }

//     public static MethodInfo GetTargetFunctionMethod(this FunctionContext context)
//     {
//         // This contains the fully qualified name of the method
//         // E.g. IsolatedFunctionAuth.TestFunctions.ScopesAndAppRoles
//         var entryPoint = context.FunctionDefinition.EntryPoint;

//         var assemblyPath = context.FunctionDefinition.PathToAssembly;
//         var assembly = Assembly.LoadFrom(assemblyPath);
//         var typeName = entryPoint.Substring(0, entryPoint.LastIndexOf('.'));
//         var type = assembly.GetType(typeName);
//         var methodName = entryPoint.Substring(entryPoint.LastIndexOf('.') + 1);
//         var method = type.GetMethod(methodName);
//         return method;
//     }
// }
