using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using McpAuthorizationSample;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Server.Tests;

public sealed class ProtocolTests
{
    [Fact]
    public async Task Discovery_is_filtered_per_caller_on_one_shared_server()
    {
        await using var host = await Harness.Start();
        var alice = await host.Rpc("alice", "tools/list", new { });
        var bob = await host.Rpc("bob", "tools/list", new { });
        Assert.Equal(["read_document"], Names(alice));
        Assert.Equal(["read_document", "update_document"], Names(bob));
        foreach (var tool in bob.GetProperty("result").GetProperty("tools").EnumerateArray())
        {
            var properties = tool.GetProperty("inputSchema").GetProperty("properties");
            Assert.False(properties.TryGetProperty("context", out _));
            Assert.False(properties.TryGetProperty("userId", out _));
            Assert.False(properties.TryGetProperty("tenantId", out _));
        }
        Assert.Equal(new StoreSnapshot(0, 0, "Alpha original", "Beta original"), host.Store.Snapshot());
    }

    [Theory]
    [InlineData(null, 401)]
    [InlineData("invalid", 401)]
    [InlineData("missing-sub", 403)]
    public async Task Http_gate_rejects_missing_invalid_or_incomplete_identity(string? user, int status)
    {
        await using var host = await Harness.Start();
        var before = host.Store.Snapshot();
        using var response = await host.Send(user, "tools/call", new { name = "update_document", arguments = new { documentId = "beta-note", text = "DENIED" } });
        Assert.Equal((HttpStatusCode)status, response.StatusCode);
        Assert.Equal("", await response.Content.ReadAsStringAsync());
        Assert.Equal(before, host.Store.Snapshot());
    }

    [Fact]
    public async Task Tool_policy_fails_closed_without_endpoint_gate_when_subject_is_missing()
    {
        // Test-only composition removes the outer gate to exercise the inner policy independently.
        await using var host = await Harness.Start(requireEndpointAuthorization: false);
        var before = host.Store.Snapshot();
        var listing = await host.Rpc("missing-sub", "tools/list", new { });
        Assert.Empty(Names(listing));
        AssertDenied(await host.Call("missing-sub", "update_document", "beta-note", "DENIED"));
        Assert.Equal(before, host.Store.Snapshot());
    }

    [Fact]
    public async Task Hidden_tool_cannot_be_called_directly_even_with_forged_identity_arguments()
    {
        await using var host = await Harness.Start();
        var before = host.Store.Snapshot();
        // Deliberately skip tools/list: filtering must not be the security boundary.
        var result = await host.Rpc("alice", "tools/call", new
        {
            name = "update_document",
            arguments = new { documentId = "alpha-note", text = "DENIED", userId = "bob", tenantId = "beta" }
        });
        AssertDenied(result);
        Assert.Equal(before, host.Store.Snapshot());
    }

    [Theory]
    [InlineData("alice", "read_document", "beta-note")]
    [InlineData("bob", "read_document", "alpha-note")]
    [InlineData("bob", "update_document", "alpha-note")]
    [InlineData("bob", "update_document", "unknown")]
    public async Task Resource_denials_never_enter_protected_operations(string user, string tool, string id)
    {
        await using var host = await Harness.Start();
        var before = host.Store.Snapshot();
        var result = await host.Rpc(user, "tools/call", new
        {
            name = tool, arguments = new { documentId = id, text = "DENIED", tenantId = "beta", userId = "bob" }
        });
        AssertDenied(result);
        Assert.Equal(before, host.Store.Snapshot());
    }

    [Fact]
    public async Task Authorized_operations_work_and_do_not_leak_the_other_callers_identity()
    {
        await using var host = await Harness.Start();
        Assert.Contains("Alpha original", (await host.Call("alice", "read_document", "alpha-note")).GetRawText());
        Assert.Contains("Updated", (await host.Call("bob", "update_document", "beta-note", "Bob changed beta")).GetRawText());
        Assert.Contains("Bob changed beta", (await host.Call("bob", "read_document", "beta-note")).GetRawText());
        var before = host.Store.Snapshot();
        AssertDenied(await host.Call("alice", "update_document", "beta-note", "DENIED"));
        Assert.Equal(before, host.Store.Snapshot());
        Assert.Equal(new StoreSnapshot(2, 1, "Alpha original", "Bob changed beta"), before);
    }

    private static string[] Names(JsonElement result) => result.GetProperty("result").GetProperty("tools")
        .EnumerateArray().Select(t => t.GetProperty("name").GetString()!).Order().ToArray();

    private static void AssertDenied(JsonElement result)
    {
        Assert.Equal(-32600, result.GetProperty("error").GetProperty("code").GetInt32());
        Assert.Contains("forbidden", result.GetProperty("error").GetProperty("message").GetString());
        Assert.False(result.TryGetProperty("result", out _));
    }
}

internal sealed class Harness(WebApplication app, HttpClient client) : IAsyncDisposable
{
    public DocumentStore Store => app.Services.GetRequiredService<DocumentStore>();
    public static async Task<Harness> Start(bool requireEndpointAuthorization = true)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();
        builder.Services.AddAuthentication("SyntheticTestsOnly")
            .AddScheme<AuthenticationSchemeOptions, TestAuthentication>("SyntheticTestsOnly", _ => { });
        builder.Services.AddSampleAuthorization();
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        var endpoint = app.MapMcp("/mcp");
        if (requireEndpointAuthorization) endpoint.RequireAuthorization();
        await app.StartAsync();
        return new(app, app.GetTestClient());
    }

    public Task<JsonElement> Call(string user, string tool, string id, string text = "") =>
        Rpc(user, "tools/call", new { name = tool, arguments = new { documentId = id, text } });

    public async Task<HttpResponseMessage> Send(string? user, string method, object parameters)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp");
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        request.Headers.Add("MCP-Protocol-Version", "2025-11-25");
        if (user is not null) request.Headers.Authorization = new("Bearer", user);
        request.Content = JsonContent.Create(new { jsonrpc = "2.0", id = 1, method, @params = parameters });
        return await client.SendAsync(request);
    }

    public async Task<JsonElement> Rpc(string user, string method, object parameters)
    {
        // Exercise initialize through the same HTTP/JSON-RPC boundary, not tool methods.
        using var initialize = await Send(user, "initialize", new
        {
            protocolVersion = "2025-11-25", capabilities = new { }, clientInfo = new { name = "authorization-tests", version = "1.0" }
        });
        initialize.EnsureSuccessStatusCode();
        var handshake = await Parse(initialize);
        Assert.True(handshake.GetProperty("result").TryGetProperty("protocolVersion", out _));
        using var response = await Send(user, method, parameters);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await Parse(response);
        Assert.Equal("2.0", result.GetProperty("jsonrpc").GetString());
        Assert.Equal(1, result.GetProperty("id").GetInt32());
        return result;
    }

    private static async Task<JsonElement> Parse(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        if (response.Content.Headers.ContentType?.MediaType == "text/event-stream")
            body = string.Join("\n", body.Split('\n').Where(l => l.StartsWith("data: ")).Select(l => l[6..]));
        using var json = JsonDocument.Parse(body);
        return json.RootElement.Clone();
    }

    public async ValueTask DisposeAsync() { client.Dispose(); await app.DisposeAsync(); }
}
