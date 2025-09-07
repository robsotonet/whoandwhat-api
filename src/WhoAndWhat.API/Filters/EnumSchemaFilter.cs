using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.ComponentModel;

namespace WhoAndWhat.API.Filters;

/// <summary>
/// Swagger schema filter to provide better enum documentation
/// </summary>
public class EnumSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type.IsEnum)
        {
            schema.Enum.Clear();
            
            var enumValues = new List<IOpenApiAny>();
            var enumDescriptions = new List<string>();

            foreach (var enumValue in Enum.GetValues(context.Type))
            {
                var enumName = enumValue.ToString()!;
                enumValues.Add(new OpenApiString(enumName));

                // Get description from DescriptionAttribute if available
                var fieldInfo = context.Type.GetField(enumName);
                var descriptionAttribute = fieldInfo?.GetCustomAttributes(typeof(DescriptionAttribute), false)
                    .Cast<DescriptionAttribute>()
                    .FirstOrDefault();

                var description = descriptionAttribute?.Description ?? enumName;
                enumDescriptions.Add($"- **{enumName}**: {description}");
            }

            schema.Enum = enumValues;
            schema.Description = $"Available values:\n\n{string.Join("\n", enumDescriptions)}";
        }
    }
}