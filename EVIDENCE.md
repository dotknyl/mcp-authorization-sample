# Verification evidence

## Publication verification — 2026-11-10

The final publication pass reviewed all sample source, tests, lock files, README and both evidence documents. No blocking security defect or credential was found in the intended publication set. Review was performed by the implementation assistant, not an independent auditor. No Git history existed to migrate. Local article drafts, API response/request JSON and the article-preparation script are excluded and unchanged, along with generated binaries, build intermediates and TRX files. Local paths and editorial job metadata were removed from published evidence. No article, platform or website was modified or published.

Added MIT licensing for original sample content, a source-only ignore list, SHA-pinned GitHub Actions for Ubuntu/Windows, and explicit framework-dependent runtime 10.0.12 pinning with roll-forward disabled. SDK 10.0.401 and exact package ranges/lock files are preserved. Locked NuGet package license metadata was inspected: MIT and Apache-2.0 (xunit.abstractions uses a legacy xUnit license URL). No dependency binaries are included. This license compatibility check is not a dependency vulnerability audit.

Executed from the repository root after these changes, Windows 11 / SDK 10.0.401 / shared runtimes 10.0.12:

```sh
dotnet restore McpAuthorization.slnx --locked-mode
dotnet build McpAuthorization.slnx --configuration Release --no-restore
dotnet test McpAuthorization.slnx --configuration Release --no-build --logger "trx;LogFileName=publication-tests.trx" --results-directory TestResults
dotnet publish src/Server/Server.csproj --configuration Release --no-build --output artifacts/publication-publish
```

Results: locked restore succeeded; Release build **0 warnings, 0 errors**; integration tests **11 passed, 0 failed, 0 skipped** (553 ms reported); framework-dependent publish succeeded. Generated evidence is local and ignored. CI runs the same checks on Ubuntu and Windows, using protocol-tests.trx and artifacts/publish paths; its actual status is available under the repository Actions tab, not inferred from these local results.

Authentication remains synthetic in tests; Program.cs JWT/OAuth integration is not verified. Coverage remains sequential and in-process, not an exhaustive concurrency, transport or interoperability test. No production-readiness or independent-audit claim is made.

## Historical implementation evidence

## Result

Executed locally on **2026-11-10**, Windows 11 (build 26200), .NET SDK **10.0.401**, Microsoft.NETCore.App / Microsoft.AspNetCore.App **10.0.12**, win-x64.

Final verification: **11 passed, 0 failed, 0 skipped**. Release build: **0 warnings, 0 errors**. Locked restore and framework-dependent server publish succeeded. No paid requests, model calls, live identity-provider calls or deployment occurred.

The sample is standalone. No platform or website source changes were made. The original verification below predates repository publication; see the publication verification section for the later checks.

## Exact dependencies

| Direct dependency | Exact version |
| --- | --- |
| ModelContextProtocol.AspNetCore | 2.4.0 |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.12 |
| Microsoft.AspNetCore.TestHost | 10.0.12 |
| Microsoft.NET.Test.Sdk | 17.14.1 |
| xunit | 2.9.3 |
| xunit.runner.visualstudio | 3.1.4 |

Resolved official SDK chain: `ModelContextProtocol.AspNetCore`, `ModelContextProtocol`, `ModelContextProtocol.Core`, all **2.4.0**. Exact direct ranges are in csproj files; both packages.lock.json files contain all resolved transitive versions and content hashes. `dotnet list McpAuthorization.slnx package --include-transitive` was also executed to inspect resolved dependencies.

## Official-source verification

NuGet's [official package index](https://api.nuget.org/v3-flatcontainer/modelcontextprotocol.aspnetcore/index.json) returned 2.4.0 as the latest listed stable version when checked. GitHub's official tag API resolved `v2.4.0` to commit **4cbf42ef107763f0bc75c358af8cb2dfe47a769a**. APIs and behavior were checked against these pinned sources before implementation:

- [HTTP builder extensions](https://github.com/modelcontextprotocol/csharp-sdk/blob/4cbf42ef107763f0bc75c358af8cb2dfe47a769a/src/ModelContextProtocol.AspNetCore/HttpMcpServerBuilderExtensions.cs): `WithHttpTransport` and `AddAuthorizationFilters` registration.
- [Authorization filter implementation](https://github.com/modelcontextprotocol/csharp-sdk/blob/4cbf42ef107763f0bc75c358af8cb2dfe47a769a/src/ModelContextProtocol.AspNetCore/AuthorizationFilterSetup.cs): list filtering, independent call checks, `McpProtocolException` / `McpErrorCode.InvalidRequest` on denial, user from request context.
- [Authorization guard setup](https://github.com/modelcontextprotocol/csharp-sdk/blob/4cbf42ef107763f0bc75c358af8cb2dfe47a769a/src/ModelContextProtocol.AspNetCore/AuthorizationCallToolFilterGuardSetup.cs): SDK checks for authorization metadata/filter configuration.
- [Official authorization tests](https://github.com/modelcontextprotocol/csharp-sdk/blob/4cbf42ef107763f0bc75c358af8cb2dfe47a769a/tests/ModelContextProtocol.AspNetCore.Tests/AuthorizeAttributeTests.cs): registration patterns, filtered listings and denied invocation expectations.
- [Request context](https://github.com/modelcontextprotocol/csharp-sdk/blob/4cbf42ef107763f0bc75c358af8cb2dfe47a769a/src/ModelContextProtocol.Core/Server/RequestContext.cs) and [tool binding documentation](https://github.com/modelcontextprotocol/csharp-sdk/blob/4cbf42ef107763f0bc75c358af8cb2dfe47a769a/src/ModelContextProtocol.Core/Server/McpServerTool.cs): SDK-injected request context, not a caller JSON argument.
- [HTTP transport options](https://github.com/modelcontextprotocol/csharp-sdk/blob/4cbf42ef107763f0bc75c358af8cb2dfe47a769a/src/ModelContextProtocol.AspNetCore/HttpServerTransportOptions.cs): explicit `HttpServerSessionMode.Stateless`.
- [Official filter guide](https://github.com/modelcontextprotocol/csharp-sdk/blob/4cbf42ef107763f0bc75c358af8cb2dfe47a769a/docs/concepts/filters.md): discovery filtering and individual-operation JSON-RPC denial behavior. The implementation, not the guide's more general `McpException` wording, supplies the exact exception type used here.

Compilation and wire-level tests then verified those APIs against the restored pinned packages. The source was read remotely, not vendored; all sample implementation files are original.

## Commands and observed outcomes

From the sample root:

```sh
dotnet restore McpAuthorization.slnx --locked-mode
dotnet build McpAuthorization.slnx -c Release --no-restore
dotnet test McpAuthorization.slnx -c Release --no-build --logger "trx;LogFileName=protocol-tests.trx" --results-directory TestResults
dotnet publish src/Server/Server.csproj -c Release --no-build -o artifacts/publish
```

Final observed output:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
Passed! - Failed: 0, Passed: 11, Skipped: 0, Total: 11
Server -> <sample-root>/artifacts/publish/
```

Raw local test evidence: `TestResults/protocol-tests.trx` (generated, gitignored). Reported final test duration: 570 ms. Earlier initial build failed with a missing `Microsoft.Extensions.Logging` import in the test authentication file; that import was fixed. The next build and original 10 tests passed; the final run includes an additional defense-in-depth test. There were no failing executed test cases.

## Assertions supported by tests

| Test cases | Evidence |
| --- | --- |
| 1 filtered-discovery case | Same server exposes read only to alice; read/write to bob. Request context and identity/tenant are absent from tool argument schemas. Discovery itself does not touch protected data. |
| 3 HTTP-gate cases | No credential and invalid credential yield HTTP 401; authenticated missing-sub principal yields HTTP 403. Bodies are empty, not JSON-RPC tool results. All snapshot fields unchanged. |
| 1 inner-policy case | With endpoint gate deliberately omitted in the test composition only, authenticated missing-sub principal sees no tools and receives JSON-RPC -32600 for an update. Snapshot unchanged. |
| 1 hidden-tool case | Alice directly invokes update without discovery, supplying forged userId/tenantId. HTTP 200 contains JSON-RPC -32600, no result. Snapshot unchanged. |
| 4 resource-denial cases | Both cross-tenant read directions, cross-tenant write and unknown-resource write denied before protected methods; full snapshots unchanged. |
| 1 positive/isolation case | Alice reads alpha; bob updates and reads beta; subsequent alice write denied. Exactly two protected reads and one protected write, alpha unchanged and beta updated only by the allowed call. |

Tenant denial happens inside a tool authorization wrapper, before entry into `ReadProtected` / `UpdateProtected`. Per-tool denial happens before invoking the tool. The counters prove these specific protected methods were not entered; they are not a claim that authorization/transport handlers did no work.

## Safe article claims and limits

Supported: in this sample, discovery filtering is not the security boundary; invocation authorization is independent. Tool permission does not grant tenant access. Identity comes from authenticated context, not tool arguments. Denied paths were exercised through HTTP/JSON-RPC and had no protected-operation entry or data mutation.

Not supported: "MCP automatically makes tools tenant-safe", "all MCP denials are HTTP 403", "-32600 is a standard authorization-specific code", "JWT/OAuth production security validated", "no code runs on denial", or "tests prove production readiness".

The production-shaped executable configures JWT validation but tests use a separate test host and test-only authentication assembly. No real token validation or identity-provider interoperability was tested. TestServer exercises the in-process HTTP/protocol boundary, not real TCP/TLS or reverse proxies. The raw test client is intentionally small and does not implement a full MCP lifecycle/client compatibility suite. Store/tenant metadata is fixed and in-memory; mutable ownership and distributed systems need transaction-aware enforcement. Other production omissions are listed in README.md.

An attempted independent review agent could not start because the host's requested isolation worktree failed its safety check. No independent review is claimed; no isolation setting was bypassed. Implementation was checked directly against pinned source and the executed tests.
