using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;

namespace McpAuthorizationSample;

public static class SampleAuthorization
{
    public const string Read = "documents.read";
    public const string Write = "documents.write";

    public static IServiceCollection AddSampleAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.DefaultPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser().RequireAssertion(ctx => HasSubject(ctx.User)).Build();
            foreach (var permission in new[] { Read, Write })
            {
                options.AddPolicy(permission, policy => policy.RequireAuthenticatedUser()
                    .RequireAssertion(ctx => HasSubject(ctx.User))
                    .RequireClaim("permission", permission));
            }
        });
        services.AddSingleton<DocumentStore>();
        services.AddMcpServer()
            .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
            .WithTools<DocumentTools>()
            .AddAuthorizationFilters();
        return services;
    }

    // No identity, permission or tenant is accepted from tool arguments.
    public static bool HasSubject(ClaimsPrincipal? user) =>
        user?.Identity?.IsAuthenticated == true &&
        user.FindAll("sub").Count() == 1 &&
        !string.IsNullOrWhiteSpace(user.FindFirst("sub")?.Value);
}
