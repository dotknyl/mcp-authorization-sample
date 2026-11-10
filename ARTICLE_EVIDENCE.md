# Article evidence: MCP authorization

**Article:** MCP Authorization in Practice: Per-Tool Permissions, User Identity, and Denied-Call Tests

**Verification date:** 2026-11-10

**Scope:** Historical pre-publication implementation verification. Editorial job metadata and local workstation paths are intentionally omitted. Later packaging/runtime-pin changes and execution results are recorded in EVIDENCE.md; this pass did not publish or revise an article.

## Verdict and provenance

Suitable as a **bounded, reproducible educational sample**, not as evidence of production readiness. Current source was reread, security-sensitive paths inspected against pinned official SDK source, and restore/build/all 11 tests/publish rerun successfully. No blocking authorization defect was identified in the current two-tool, single-authentication-scheme composition.

This is a fresh verification pass by the implementation assistant, **not an independent reviewer sign-off**. A separate read-only review agent was attempted but could not start because the host isolation-worktree safety check failed. The safeguard was not bypassed. No independent-review or security-certification claim is warranted.

README.md, EVIDENCE.md, all server code, synthetic authentication and all 11 test cases were reviewed. No implementation, platform or website source was changed in this pass. This file does not revise, approve or publish the Article Job. The existing article text was not supplied and was not found by a targeted search of the local AgentProbe Markdown/JSON content; recommendations below target the described PermissionHarness, not verified article line numbers.

## 1. Exact environment and dependencies

Observed: Windows 11 build 26200, win-x64; .NET SDK **10.0.401**, MSBuild **18.9.11**, host and installed ASP.NET Core/.NET runtime **10.0.12**. Target: `net10.0`. `global.json` disables SDK roll-forward; this does not pin the deployment runtime image.

| Direct package | Exact csproj pin | Project |
| --- | --- | --- |
| ModelContextProtocol.AspNetCore | `[2.4.0]` | src/Server |
| Microsoft.AspNetCore.Authentication.JwtBearer | `[10.0.12]` | src/Server |
| Microsoft.AspNetCore.TestHost | `[10.0.12]` | tests/Server.Tests |
| Microsoft.NET.Test.Sdk | `[17.14.1]` | tests/Server.Tests |
| xunit | `[2.9.3]` | tests/Server.Tests |
| xunit.runner.visualstudio | `[3.1.4]` | tests/Server.Tests |

Both project `packages.lock.json` files were inspected; locked restore succeeded. The SDK chain resolves `ModelContextProtocol.AspNetCore`, `ModelContextProtocol` and `ModelContextProtocol.Core` to **2.4.0**. Other resolved versions include IdentityModel **8.19.2** and Microsoft.Extensions.AI.Abstractions **10.11.0**; the test graph also uses Microsoft.Extensions abstractions **10.0.14**. Lock files, not a claim that every dependency matches the runtime patch, specify the complete graph. NuGet.Config restricts package sources to nuget.org. No dependency vulnerability audit was performed.

## 2. Reproduce and actual results

Run from the sample root, with SDK 10.0.401 installed:

```sh
dotnet --info
dotnet restore McpAuthorization.slnx --locked-mode
dotnet build McpAuthorization.slnx -c Release --no-restore
dotnet test McpAuthorization.slnx -c Release --no-build --logger "trx;LogFileName=article-verification.trx" --results-directory TestResults
dotnet publish src/Server/Server.csproj -c Release --no-build -o artifacts/article-verification-publish
```

All commands above executed successfully in this pass. Release build: **0 warnings, 0 errors**. Test output: **11 passed, 0 failed, 0 skipped**, reported duration approximately **1 second**. The generated `TestResults/article-verification.trx` independently records `total="11" executed="11" passed="11" failed="0" notExecuted="0"`. Publish produced `artifacts/article-verification-publish/`. Generated files are local and gitignored.

No paid APIs, models, live identity provider, deployment or production token validation were exercised. Source was not changed between review and the rerun.

### Reviewed source fingerprints

SHA-256 of key current files; retain with the evidence when transferring outside this directory. There is no sample commit SHA to cite.

```text
15ed37dc67b9a1d47b3cfa4685259543714e6fa3ad153e2485dc0f746ab6878d  src/Server/Program.cs
b22916103d409bdca63bde3344747b46676b542713213be47611ad7d04c49ff6  src/Server/SampleAuthorization.cs
ba2868c96e424190c19d01e65321ec58ae538112aa87c49fa884bf06e485bfd8  src/Server/DocumentTools.cs
110f0ce586eb95927d362aeda2dba6091c2dd8ebc970d71fe3b42827677d1fe0  src/Server/DocumentStore.cs
6716e437dda6f19cbcfda96e97e3aac35002251bbd04c2b20e25982d60c72901  tests/Server.Tests/TestAuthentication.cs
59195392f6eab93231eba8de333519546860d0093ff8f5e017c3478d7c4fe274  tests/Server.Tests/ProtocolTests.cs
bfce896bc93a7e502baddb81b1f10e336cddd3525bee0ec037c9279756b0d13f  src/Server/packages.lock.json
7dd5b1848542de1c0bbd8d9b13d938ac45b0719009d0cf6d199d637437861a50  tests/Server.Tests/packages.lock.json
```

## 3. Verified architecture and authorization flow

One ASP.NET Core host exposes `/mcp` with the official SDK's Streamable HTTP transport in explicit **stateless** mode; callers share the host and singleton store, not cached caller identity. A stateless host may create per-request MCP server machinery: do not imply one long-lived MCP session.

1. **HTTP authentication:** Program.cs configures JWT bearer validation, disables claim mapping and requires HTTPS authority metadata, issuer, audience, lifetime, signing key, signed tokens and expiration. Missing HTTPS authority/nonblank audience fails startup. These settings were read, not tested with real JWTs.
2. **HTTP authorization:** `MapMcp("/mcp").RequireAuthorization()` uses a policy requiring authentication and exactly one nonblank `sub`.
3. **Identity propagation:** the SDK's `StreamableHttpHandler` copies authenticated `HttpContext.User` into `message.Context.User`; `MessageContext.User`, inherited by `RequestContext<T>`, reads that context. The injected request context is not a JSON tool argument.
4. **Per-tool authorization:** both tools have named `[Authorize]` policies requiring subject plus their own `permission` claim. `AddAuthorizationFilters()` filters `tools/list` and checks `tools/call` independently before tool execution. There is no reliance on a client first listing tools.
5. **Resource authorization:** the tool wrapper gets the document's tenant from fixed server-owned metadata and requires a matching trusted `tenant` claim before calling protected content methods. A caller-supplied `tenantId` or `userId` cannot choose the principal or bypass this check. Unknown and inaccessible documents use the same outward error.
6. **Observation:** the store increments counters inside `ReadProtected`/`UpdateProtected`, under the same lock as content access. Negative tests compare counters and both contents before/after; positive tests establish that allowed calls do increment them.

Synthetic test users: **alice = read + alpha**, **bob = read/write + beta**. Bob is not a global administrator. The malformed `missing-sub` fixture is a negative principal, not a third business user. Synthetic credentials exist only in the test assembly; there is no executable setting enabling them.

## 4. Concise implementation excerpts

Paths are relative to the sample root; line numbers are 1-based. Excerpts omit unrelated code but do not invent APIs.

**Registration — src/Server/SampleAuthorization.cs:19–30**

```csharp
foreach (var permission in new[] { Read, Write })
{
    options.AddPolicy(permission, policy => policy.RequireAuthenticatedUser()
        .RequireAssertion(ctx => HasSubject(ctx.User))
        .RequireClaim("permission", permission));
}
// ...
services.AddMcpServer()
    .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
    .WithTools<DocumentTools>()
    .AddAuthorizationFilters();
```

**Policy-bound tool and resource boundary — src/Server/DocumentTools.cs:20–43**

```csharp
[McpServerTool(Name = "update_document"), Authorize(Policy = SampleAuthorization.Write)]
[Description("Replace a document if the authenticated caller has access to its tenant.")]
public string UpdateDocument(RequestContext<CallToolRequestParams> context, string documentId, string text)
{
    var document = AuthorizeDocument(context, documentId);
    return store.UpdateProtected(document.Id, text);
}
// Inside AuthorizeDocument:
var user = context.User;
if (!SampleAuthorization.HasSubject(user))
    throw Denied();
var metadata = store.FindMetadata(id);
if (metadata is null || !user!.HasClaim("tenant", metadata.Tenant))
    throw Denied();
return metadata;
// Denied(): new McpProtocolException(
//   "Access forbidden: document unavailable.", McpErrorCode.InvalidRequest)
```

**Protected-operation instrumentation — src/Server/DocumentStore.cs:26–39**

```csharp
internal string UpdateProtected(string id, string value)
{
    lock (gate) { writes++; text[id] = value; return "Updated"; }
}
public StoreSnapshot Snapshot()
{
    lock (gate) { return new(reads, writes, text["alpha-note"], text["beta-note"]); }
}
```

**Direct denied call through MCP — tests/Server.Tests/ProtocolTests.cs:61–73**

```csharp
var before = host.Store.Snapshot();
var result = await host.Rpc("alice", "tools/call", new
{
    name = "update_document",
    arguments = new { documentId = "alpha-note", text = "DENIED", userId = "bob", tenantId = "beta" }
});
AssertDenied(result);
Assert.Equal(before, host.Store.Snapshot());
```

`Rpc` sends `initialize` and then the requested JSON-RPC method over `HttpClient` into TestServer, asserts HTTP 200, parses JSON/SSE and checks response ID/version (ProtocolTests.cs:140–175). `AssertDenied` asserts error `-32600`, a forbidden message and absence of `result` (109–113). This is an HTTP/MCP boundary test, not a direct service-method test; the local class named `Harness` is test-host plumbing, not the article's standalone PermissionHarness.

## 5. All 11 executed test cases

All paths below refer to tests/Server.Tests/ProtocolTests.cs; every row passed.

| Location | Cases | Verified behavior |
| --- | ---: | --- |
| 17–32 | 1 | Alice lists read only; bob lists read/write on the same host. Context/userId/tenantId absent from schemas; zero protected access. |
| 35–46 | 3 | Missing credential → HTTP 401; invalid credential → HTTP 401; authenticated missing-sub → HTTP 403. Empty bodies; snapshots unchanged. |
| 49–58 | 1 | Test-only omission of endpoint gate: missing-sub sees no tools and direct update gets JSON-RPC denial; snapshot unchanged. |
| 61–73 | 1 | Alice directly calls hidden update without listing, with forged identity arguments; denied; snapshot unchanged. |
| 76–90 | 4 | Alice read beta; bob read alpha; bob update alpha; bob update unknown: denied before protected access; snapshots unchanged. |
| 93–103 | 1 | Alice reads alpha; bob updates/reads beta; later alice update denied. Final snapshot: reads=2, writes=1, alpha original, beta changed by bob. |

**Observed error distinction:** HTTP authentication failures are not MCP tool errors. An authenticated but incomplete identity fails the HTTP authorization policy with 403. After the HTTP gate, tool-policy and application tenant denials return **HTTP 200 + JSON-RPC `error.code = -32600`**, with no `result`. This is the pinned SDK/application behavior; `-32600` is named `InvalidRequest`, not a universal MCP authorization-specific code. Do not conflate it with `result.isError` tool execution failures.

## 6. Issues, evidence gaps and production limits

No blocking defect was found in the reviewed bounded composition. Publication must preserve these qualifications:

- **No independent sign-off.** A fresh execution and source inspection happened; the separate reviewer did not run.
- **No JWT integration verification.** Tests compose a separate host with synthetic authentication and shared authorization/MCP registration. They do not execute Program.cs. Issuer/signature/audience handling, token acquisition and issuer claim mapping are unverified at runtime.
- **Not globally deny-by-default for future tools.** Official filters allow primitives without authorization metadata. Both present tools are annotated; every added tool needs a policy and any required resource check. The endpoint alone only requires authentication/subject.
- **Single trusted identity assumption.** Permission/tenant claim queries scan the ClaimsPrincipal, not an explicitly selected authenticated identity. This composition has one authentication scheme/identity and no claim enrichment. Multi-scheme principals, claims transformations or untrusted appended identities require a fresh provenance review; do not generalize this sample to them.
- **Positive assertions are coarse.** Success tests search serialized result text and validate store counters/content; they do not explicitly assert absence of `error` or `result.isError == false`. A useful follow-up is structured positive-result assertions. Current negative tests do assert the exact error code and no result.
- **Fail-closed cases are not exhaustive.** No dedicated blank/duplicate-sub, missing-tenant, missing-read-permission or malformed-argument cases. The current policy/check code rejects absent/blank/duplicate subjects and missing tenant matches, but only missing-sub is exercised. Forged extra arguments do not establish schema rejection of every unexpected argument.
- **Counter scope matters.** Unchanged snapshots establish non-entry into the specific protected store methods and no document mutation. Tenant denials still enter a tool authorization wrapper and perform metadata lookup; per-tool denials precede the tool. No claim that "no handler/code ran" is supported. No separate tool-entry counter was added.
- **Transport scope is limited.** TestServer runs HTTP middleware and SDK serialization/dispatch in process, not TCP/TLS/proxies. The helper offers JSON and SSE but does not test each independently, validate exact negotiated protocol revision, send a complete general-client lifecycle or establish interoperability with external MCP clients. Requests specify 2025-11-25; handshake only checks that a version was returned.
- **Fixed data and sequential callers.** Immutable tenant metadata avoids ownership-change races here. Mutable/distributed data needs transactional or row-level enforcement; counters and locks are process-local. Alternating users were tested, not concurrent identity changes or stateful session ownership.
- **No production hardening claim.** No OAuth protected-resource metadata/discovery/consent, revocation, live IdP, rate limiting, ingress/Origin configuration, audit pipeline, persistence, dependency audit or adversarial protocol fuzzing was verified. Never deploy the synthetic scheme.

These are scope limits and follow-up opportunities, not claims that the two existing tools are currently bypassable. No source changes were made to conceal or broaden the verified test coverage.

## 7. Official references

Use immutable SDK revision **4cbf42ef107763f0bc75c358af8cb2dfe47a769a** (v2.4.0), not a moving main branch. References S1–S3 were fetched and inspected again in this pass; the others are supporting references recorded in the original evidence.

- **S1 — actual authorization implementation:** [AuthorizationFilterSetup.cs](https://github.com/modelcontextprotocol/csharp-sdk/blob/4cbf42ef107763f0bc75c358af8cb2dfe47a769a/src/ModelContextProtocol.AspNetCore/AuthorizationFilterSetup.cs). Separate list/call filters, policy evaluation, -32600 denial and behavior without metadata.
- **S2 — HTTP principal propagation:** [StreamableHttpHandler.cs](https://github.com/modelcontextprotocol/csharp-sdk/blob/4cbf42ef107763f0bc75c358af8cb2dfe47a769a/src/ModelContextProtocol.AspNetCore/StreamableHttpHandler.cs), near `message.Context.User = context.User`.
- **S3 — context principal accessor:** [MessageContext.cs](https://github.com/modelcontextprotocol/csharp-sdk/blob/4cbf42ef107763f0bc75c358af8cb2dfe47a769a/src/ModelContextProtocol.Core/Server/MessageContext.cs), `User` backed by JSON-RPC message context.
- **S4 — public registration APIs:** [HttpMcpServerBuilderExtensions.cs](https://github.com/modelcontextprotocol/csharp-sdk/blob/4cbf42ef107763f0bc75c358af8cb2dfe47a769a/src/ModelContextProtocol.AspNetCore/HttpMcpServerBuilderExtensions.cs).
- **S5 — official explanatory documentation:** [filters.md, authorization section](https://github.com/modelcontextprotocol/csharp-sdk/blob/4cbf42ef107763f0bc75c358af8cb2dfe47a769a/docs/concepts/filters.md). Prefer S1 for exact exception/error behavior.
- **S6 — request-context injection contract:** [McpServerTool.cs](https://github.com/modelcontextprotocol/csharp-sdk/blob/4cbf42ef107763f0bc75c358af8cb2dfe47a769a/src/ModelContextProtocol.Core/Server/McpServerTool.cs) and [RequestContext.cs](https://github.com/modelcontextprotocol/csharp-sdk/blob/4cbf42ef107763f0bc75c358af8cb2dfe47a769a/src/ModelContextProtocol.Core/Server/RequestContext.cs).
- **S7 — official SDK regression examples:** [AuthorizeAttributeTests.cs](https://github.com/modelcontextprotocol/csharp-sdk/blob/4cbf42ef107763f0bc75c358af8cb2dfe47a769a/tests/ModelContextProtocol.AspNetCore.Tests/AuthorizeAttributeTests.cs).

## 8. Precise article revision plan

1. **Replace the standalone PermissionHarness as the principal worked example.** Introduce the real sample project tree: server Program/registration/tools/store plus TestAuthentication and ProtocolTests. Keep any conceptual permission example only as optional pseudocode, never as the evidence for MCP enforcement.
2. **Show the actual boundary in order:** JWT-shaped HTTP authentication → endpoint policy → SDK request principal → named tool policy → server-owned document tenant check → protected operation. Use excerpts above and S1–S3; do not say an LLM or tool argument establishes identity.
3. **Use the alice/bob matrix.** State explicitly that bob can update beta but cannot read/update alpha. Explain that tool-level write permission and resource-level tenant access are separate requirements.
4. **Replace custom discovery logic with the verified SDK API.** Show `.WithTools<DocumentTools>().AddAuthorizationFilters()` and the actual `[Authorize(Policy = ...)]` annotations. Explain why this does not protect newly added unannotated tools automatically.
5. **Replace direct-method denied tests with the HTTP `tools/call` excerpt.** Show the snapshot assertion and identify what its counters measure. Add cross-tenant write and the positive-counter control; distinguish tenant wrapper execution from protected-store entry.
6. **Replace a universal "403 on denied tool" claim, if present, with the observed three-way distinction:** HTTP 401 authentication, HTTP 403 endpoint-policy failure, HTTP 200 with JSON-RPC -32600 after MCP dispatch. Label behavior SDK/version-specific.
7. **Report evidence exactly:** locally rerun 11/11 on SDK 10.0.401, MCP SDK 2.4.0, Windows win-x64; link the sample and this evidence once a real repository URL exists. Do not invent a public repository, commit link, independent review or live OAuth result.
8. **End with the limits in section 6.** Explicitly separate synthetic test authentication from executable JWT configuration and protocol-boundary coverage from production interoperability/security.

Suggested replacement summary: “We exercised one ASP.NET Core MCP HTTP host with two synthetic authenticated callers. The official C# SDK filtered discovery and separately enforced invocation policies; application code checked server-owned document tenants before protected reads or writes. Eleven local protocol-boundary test cases passed. These results establish the demonstrated authorization paths, not a production OAuth deployment.”
