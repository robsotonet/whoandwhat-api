using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace WhoAndWhat.API.Filters;

/// <summary>
/// Swagger operation filter to handle file upload endpoints
/// </summary>
public class FileUploadOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var fileParameters = context.MethodInfo.GetParameters()
            .Where(p => p.ParameterType == typeof(IFormFile) || 
                       p.ParameterType == typeof(IFormFileCollection) ||
                       p.ParameterType.IsAssignableFrom(typeof(IEnumerable<IFormFile>)))
            .ToList();

        if (fileParameters.Any())
        {
            operation.RequestBody = new OpenApiRequestBody
            {
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["multipart/form-data"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = "object",
                            Properties = fileParameters.ToDictionary(
                                p => p.Name!,
                                p => new OpenApiSchema
                                {
                                    Type = "string",
                                    Format = "binary",
                                    Description = GetFileDescription(p.ParameterType)
                                })
                        }
                    }
                }
            };

            // Remove file parameters from the parameter list
            operation.Parameters = operation.Parameters?
                .Where(p => !fileParameters.Any(fp => fp.Name == p.Name))
                .ToList();
        }

        // Handle data export endpoints that return files
        if (context.MethodInfo.Name.Contains("Export") || context.MethodInfo.Name.Contains("Download"))
        {
            if (operation.Responses.ContainsKey("200"))
            {
                operation.Responses["200"].Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = "string",
                            Format = "binary",
                            Description = "JSON file download"
                        }
                    },
                    ["text/csv"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = "string",
                            Format = "binary",
                            Description = "CSV file download"
                        }
                    },
                    ["application/octet-stream"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = "string",
                            Format = "binary",
                            Description = "Binary file download"
                        }
                    }
                };
            }
        }
    }

    private static string GetFileDescription(Type parameterType)
    {
        if (parameterType == typeof(IFormFile))
            return "Single file upload";
        
        if (parameterType == typeof(IFormFileCollection) || 
            parameterType.IsAssignableFrom(typeof(IEnumerable<IFormFile>)))
            return "Multiple file upload";
        
        return "File upload";
    }
}