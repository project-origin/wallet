using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ProjectOrigin.Vault.Extensions;

public class SwaggerExtensions
{
    public class NormalizeDescriptions : IDocumentFilter
    {
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            foreach (var path in swaggerDoc.Paths)
            foreach (var operation in path.Value.Operations)
            {
                operation.Value.Description = Normalize(operation.Value.Description);
                operation.Value.Summary = Normalize(operation.Value.Summary);
            }
        }

        private static string? Normalize(string? input) =>
            input?
                .Replace("\r\n", " ")
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Replace("  ", " ")
                .Trim();
    }
}
