# MCP authorization in practice

Minimal standalone C# sample for a technical article: one shared ASP.NET Core MCP HTTP endpoint, per-tool permissions, trusted caller identity and resource-level tenant authorization. No model, paid service, SEO platform or website dependency.

## Reproduce

Install .NET SDK **10.0.401** (pinned without roll-forward in global.json). From this directory:

```sh
dotnet restore McpAuthorization.slnx --locked-mode
dotnet build McpAuthorization.slnx --configuration Release --no-restore
dotnet test McpAuthorization.slnx --configuration Release --no-build --logger "trx;LogFileName=protocol-tests.trx" --results-directory TestResults
dotnet publish src/Server/Server.csproj --configuration Release --no-build --output artifacts/publish
```

Prerequisites: .NET SDK **10.0.401**, .NET and ASP.NET Core shared runtimes **10.0.12**, and a shell. No identity provider is needed to run the tests. Clone https://github.com/dotknyl/mcp-authorization-sample and change into its root before running the commands above.

Both project files pin exact dependency ranges; packages.lock.json files pin transitive versions and content hashes. NuGet.Config clears inherited feeds. Directory.Build.props requests runtime **10.0.12** with runtime roll-forward disabled; global.json pins the build SDK. Publish is framework-dependent, so install both shared runtimes at that exact patch on the target machine. These pins are for reproducibility, not a recommendation to delay security updates; update SDK/runtime/package pins and rerun verification together. Build output and local TRX files are ignored, not repository source.

## Architecture and files

`HTTP authentication → endpoint subject policy → SDK request principal → tool permission policy → document tenant check → protected store operation`

- `src/Server/Program.cs`: executable JWT configuration and `/mcp` endpoint.
- `src/Server/SampleAuthorization.cs`: shared policies and stateless MCP registration.
- `src/Server/DocumentTools.cs`: explicitly authorized read/update tools and tenant checks.
- `src/Server/DocumentStore.cs`: fixed metadata, in-memory content, protected-operation counters.
- `tests/Server.Tests/`: test-only authentication and HTTP/JSON-RPC integration tests.
- `.github/workflows/ci.yml`: locked restore, Release build, all integration tests and publish on Ubuntu and Windows.

## Authorization model

| Synthetic user (test assembly only) | Permissions | Tenant |
| --- | --- | --- |
| alice | documents.read | alpha |
| bob | documents.read, documents.write | beta |

The immutable server-owned resource map assigns `alpha-note` to alpha and `beta-note` to beta. Bob is not a global administrator: write permission does not confer access to alpha.

1. ASP.NET Core authenticates each HTTP request. The endpoint default policy requires an authenticated principal with exactly one nonblank `sub`.
2. `AddAuthorizationFilters()` evaluates the policies on `[Authorize]` tool methods. Discovery hides unauthorized tools; invocation independently rejects a known tool name. A client need not discover a tool to attempt to call it.
3. SDK-injected `RequestContext<CallToolRequestParams>.User` supplies the caller. Context is not a model argument. Policies read trusted `permission` claims; resource checks read trusted `tenant` claims. `userId` or `tenantId` in arbitrary tool arguments is never authority.
4. Each tool looks up server-owned resource metadata, verifies the caller's tenant claim, then enters `ReadProtected` or `UpdateProtected`. Unknown and inaccessible IDs use the same error. The metadata lookup is not a protected document-content read.
5. Protected-operation counters are incremented inside those store methods. Negative tests assert the entire snapshot (read counter, write counter, both contents) remains unchanged. Tenant checks necessarily enter the tool's authorization wrapper, but not the protected data handler. Do not describe this as "no code ran".

## Protocol tests

Tests use ASP.NET Core TestServer: real middleware, HTTP request serialization, the official SDK HTTP transport/JSON-RPC dispatcher, filters, tool argument binding and response serialization. They send `initialize`, `tools/list` and `tools/call`, with both JSON and SSE accepted and explicit protocol revision `2025-11-25`. The transport is explicitly stateless. A small raw JSON-RPC test client makes the wire error distinction visible; it is not a replacement MCP SDK or a production client.

Tests cover filtered discovery and schemas, unauthenticated/invalid credentials, an authenticated principal missing a subject, direct calls to hidden tools, forged identity arguments, cross-tenant read/write attempts, an unknown ID, successful operations and alternating callers on one shared server. A separate test-only composition omits the HTTP endpoint authorization gate to prove the inner tool policy also rejects a missing subject.

| Failure | HTTP | MCP response |
| --- | --- | --- |
| No authentication / invalid synthetic credential | 401 | Empty body, no JSON-RPC tool response |
| Authenticated but missing `sub` at endpoint | 403 | Empty body, no JSON-RPC tool response |
| Tool permission or resource tenant denied after HTTP gate | 200 | `error.code: -32600` (`InvalidRequest`), no `result` |

`-32600` is the pinned SDK's authorization behavior, not a universal MCP "forbidden" code. These tests do not treat a `result.isError` tool execution result as equivalent to a JSON-RPC error.

## Run the executable with real authentication

The server project contains **no synthetic authentication handler**. It refuses startup without an HTTPS authority and a nonblank audience. Configure an actual OIDC issuer and API audience, for example in PowerShell (replace the illustrative values):

```powershell
$env:Authentication__Authority = 'https://issuer.example.invalid'
$env:Authentication__Audience = 'mcp-documents'
dotnet run --project src/Server --no-launch-profile -- --urls https://localhost:7443
```

Trust/configure an ASP.NET Core development certificate locally if needed. The illustrative issuer above cannot authenticate users. Tokens must be signed, unexpired and valid for the configured issuer/audience, carrying one `sub`, repeated `permission` claims and repeated `tenant` claims as appropriate. Claim assignment is the issuer's responsibility. Map your own issuer's claim contract deliberately; this sample does not infer tenants from email or accept raw permission headers.

Tests build their own host and register a test-only scheme; they share the MCP and authorization registration and endpoint wiring pattern, but **do not execute Program's JWT setup**. They verify authorization given synthetic authenticated principals, not token signature/issuer/audience validation or an OAuth deployment. There is no configuration switch enabling test authentication in the executable.

## Scope and limitations

- Local educational sample, not a production-ready OAuth resource server or security certification. No live identity-provider validation was performed.
- No OAuth authorization-server discovery/protected-resource metadata, consent, dynamic registration, token acquisition, scope negotiation, token revocation, identity lifecycle, audit pipeline, rate limiting or distributed persistence.
- TestServer does not exercise sockets, TLS, reverse proxies, ingress/Origin policy, browser CORS, deployment routing or stateful session ownership. Configure those boundaries separately; do not expose a development instance publicly.
- Mutable tenant membership/resource ownership needs transactional authorization or row-level controls. Here resource tenant metadata is immutable and operations are process-local; permissions remain valid for the supplied principal's lifetime.
- Test observations are process-local and accessed through DI only, not public routes. The sample assumes tool registration/DI code is trusted. New tools must receive explicit authorization metadata and appropriate resource checks.
- Tests do not cover all MCP methods, malformed-message fuzzing, long-lived connections, concurrent principal changes or external client interoperability. No production guarantee follows from passing them.

- The composition assumes one trusted authentication scheme/identity. Claim queries span the principal; multi-identity claims transformations need a fresh provenance review.
- The SDK filters do not automatically deny future tools without authorization metadata. Both current tools are annotated; explicitly protect every new tool.
- Positive assertions inspect serialized text and store state; fail-closed and malformed-input cases are not exhaustive. The sample has not been independently audited.

See [EVIDENCE.md](EVIDENCE.md) for source verification and executed results and [ARTICLE_EVIDENCE.md](ARTICLE_EVIDENCE.md) for historical review details. The latter records a pre-publication pass, not a published article or an independent audit.

## License

Original sample source and documentation: [MIT](LICENSE). NuGet dependencies retain their own licenses: the MCP SDK and xUnit packages are Apache-2.0; Microsoft packages and Newtonsoft.Json are MIT. Dependencies are restored from NuGet, not vendored or distributed as release binaries.
