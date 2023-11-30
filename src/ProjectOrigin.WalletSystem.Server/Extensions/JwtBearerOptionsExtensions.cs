using System;
using System.Linq;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ProjectOrigin.WalletSystem.Server.Options;
using Serilog;

namespace ProjectOrigin.WalletSystem.Server.Extensions;

public static class JwtBearerOptionsExtensions
{
    public static void ConfigureJwtVerification(this JwtBearerOptions bearerOptions, JwtOptions jwtOptions)
    {

        if (jwtOptions.AllowAnyJwtToken)
        {
            Log.Warning("No JWT issuers configured. Server will accept any jwt-tokens! This is not recommended for production environments.");
            bearerOptions.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateIssuerSigningKey = false,
                ValidateAudience = false,
                ValidateActor = false,
                ValidateTokenReplay = false,
                ValidateLifetime = false,
                SignatureValidator = (token, _) => new Microsoft.IdentityModel.JsonWebTokens.JsonWebToken(token)
            };
        }
        else if (jwtOptions.Issuers.Any())
        {
            bearerOptions.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = !jwtOptions.Audience.IsEmpty(),
                TryAllIssuerSigningKeys = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                ValidateTokenReplay = false,
                RequireSignedTokens = true,
                ValidAudience = jwtOptions.Audience,
                ValidIssuers = jwtOptions.Issuers.Select(x => x.IssuerName).ToList(),
                IssuerSigningKeys = jwtOptions.Issuers.Select(x => x.SecurityKey).ToList(),
            };
        }
        else
        {
            throw new NotSupportedException($"AllowAnyJwtToken is set to ”false” and no issuers are configured!");
        }
    }
}
