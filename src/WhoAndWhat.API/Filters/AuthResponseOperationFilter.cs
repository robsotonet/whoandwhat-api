using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace WhoAndWhat.API.Filters;

/// <summary>
/// Swagger operation filter to add authentication responses to secured endpoints
/// </summary>
public class AuthResponseOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var hasAuthorizeAttribute = context.MethodInfo.GetCustomAttributes(true)
            .OfType<AuthorizeAttribute>()
            .Any() || context.MethodInfo.DeclaringType?.GetCustomAttributes(true)
            .OfType<AuthorizeAttribute>()
            .Any();

        if (hasAuthorizeAttribute)
        {
            // Add 401 Unauthorized response
            if (!operation.Responses.ContainsKey("401"))
            {
                operation.Responses.Add("401", new OpenApiResponse
                {
                    Description = "Unauthorized - Authentication required",
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType
                        {
                            Schema = new OpenApiSchema
                            {
                                Reference = new OpenApiReference
                                {
                                    Type = ReferenceType.Schema,
                                    Id = "ProblemDetails"
                                }
                            }
                        }
                    }
                });
            }

            // Add 403 Forbidden response for specific authorization requirements
            if (!operation.Responses.ContainsKey("403"))
            {
                operation.Responses.Add("403", new OpenApiResponse
                {
                    Description = "Forbidden - Insufficient permissions",
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType
                        {
                            Schema = new OpenApiSchema
                            {
                                Reference = new OpenApiReference
                                {
                                    Type = ReferenceType.Schema,
                                    Id = "ProblemDetails"
                                }
                            }
                        }
                    }
                });
            }

            // Ensure security requirement is set
            if (!operation.Security.Any())
            {
                operation.Security = new List<OpenApiSecurityRequirement>
                {
                    new OpenApiSecurityRequirement
                    {
                        {
                            new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference
                                {
                                    Type = ReferenceType.SecurityScheme,
                                    Id = "Bearer"
                                }
                            },
                            Array.Empty<string>()
                        }
                    }
                };
            }
        }

        // Add common error responses
        if (!operation.Responses.ContainsKey("429"))
        {
            operation.Responses.Add("429", new OpenApiResponse
            {
                Description = "Too Many Requests - Rate limit exceeded"
            });
        }

        if (!operation.Responses.ContainsKey("500"))
        {
            operation.Responses.Add("500", new OpenApiResponse
            {
                Description = "Internal Server Error",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.Schema,
                                Id = "ProblemDetails"
                            }
                        }
                    }
                }
            });
        }
    }
}