using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using McpAuthorizationSample;

var builder = WebApplication.CreateBuilder(args);
var authority = builder.Configuration["Authentication:Authority"];
var audience = builder.Configuration["Authentication:Audience"];
if (!Uri.TryCreate(authority, UriKind.Absolute, out var issuer) ||
    issuer.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(audience))
{
    throw new InvalidOperationException("Configure an HTTPS Authentication:Authority and Authentication:Audience.");
}
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.Audience = audience;
        options.RequireHttpsMetadata = true;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            NameClaimType = "sub",
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddSampleAuthorization();
var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapMcp("/mcp").RequireAuthorization();
app.Run();

public partial class Program;
