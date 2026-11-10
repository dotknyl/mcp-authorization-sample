using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace Server.Tests;

// Deliberately compiled ONLY into the test assembly. Not a development server scheme.
internal sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var credential = Request.Headers.Authorization.ToString();
        if (credential.Length == 0) return Task.FromResult(AuthenticateResult.NoResult());
        Claim[] claims = credential switch
        {
            "Bearer alice" => [new("sub", "alice"), new("permission", "documents.read"), new("tenant", "alpha")],
            "Bearer bob" => [new("sub", "bob"), new("permission", "documents.read"), new("permission", "documents.write"), new("tenant", "beta")],
            // Malformed authenticated principal: negative test, not a third business user.
            "Bearer missing-sub" => [new("permission", "documents.write"), new("tenant", "beta")],
            _ => []
        };
        if (claims.Length == 0) return Task.FromResult(AuthenticateResult.Fail("Unknown test credential"));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
    }
}
