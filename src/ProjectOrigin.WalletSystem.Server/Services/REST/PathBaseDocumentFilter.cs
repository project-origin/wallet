using System.Linq;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using ProjectOrigin.WalletSystem.Server.Options;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ProjectOrigin.WalletSystem.Server.Services.REST;

public class PathBaseDocumentFilter : IDocumentFilter
{
    private readonly IOptions<ServiceOptions> _options;

    public PathBaseDocumentFilter(IOptions<ServiceOptions> options)
    {
        _options = options;
    }

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        var paths = swaggerDoc.Paths.ToArray();
        foreach (var path in paths)
        {
            swaggerDoc.Paths.Remove(path.Key);
            var keyWithBasePath = $"{_options.Value.PathBase}{path.Key}";
            swaggerDoc.Paths.Add(keyWithBasePath, path.Value);
        }
    }
}
