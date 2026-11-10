using System.ComponentModel;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace McpAuthorizationSample;

[McpServerToolType]
public sealed class DocumentTools(DocumentStore store)
{
    [McpServerTool(Name = "read_document"), Authorize(Policy = SampleAuthorization.Read)]
    [Description("Read a document if the authenticated caller has access to its tenant.")]
    public string ReadDocument(RequestContext<CallToolRequestParams> context, string documentId)
    {
        var document = AuthorizeDocument(context, documentId);
        return store.ReadProtected(document.Id);
    }

    [McpServerTool(Name = "update_document"), Authorize(Policy = SampleAuthorization.Write)]
    [Description("Replace a document if the authenticated caller has access to its tenant.")]
    public string UpdateDocument(RequestContext<CallToolRequestParams> context, string documentId, string text)
    {
        var document = AuthorizeDocument(context, documentId);
        return store.UpdateProtected(document.Id, text);
    }

    private DocumentMetadata AuthorizeDocument(RequestContext<CallToolRequestParams> context, string id)
    {
        // Reached only after per-tool authorization. Metadata lookup is not a protected read.
        // Resolve the tenant from server-owned metadata, not a caller-supplied tenantId.
        var user = context.User;
        if (!SampleAuthorization.HasSubject(user))
            throw Denied();
        var metadata = store.FindMetadata(id);
        if (metadata is null || !user!.HasClaim("tenant", metadata.Tenant))
            throw Denied();
        return metadata;
    }

    // Same outward error for unknown and inaccessible resources; no tenant/id disclosure.
    private static McpProtocolException Denied() =>
        new("Access forbidden: document unavailable.", McpErrorCode.InvalidRequest);
}
