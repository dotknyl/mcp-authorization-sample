namespace McpAuthorizationSample;

public sealed record DocumentMetadata(string Id, string Tenant);
public sealed record StoreSnapshot(int ProtectedReads, int ProtectedWrites, string Alpha, string Beta);

// Fixed metadata and a single lock keep the tenant check's assumptions simple in this sample.
// A real database needs resource authorization tied to the operation/transaction (TOCTOU).
public sealed class DocumentStore
{
    private readonly object gate = new();
    private readonly Dictionary<string, DocumentMetadata> metadata = new()
    {
        ["alpha-note"] = new("alpha-note", "alpha"),
        ["beta-note"] = new("beta-note", "beta")
    };
    private readonly Dictionary<string, string> text = new()
    {
        ["alpha-note"] = "Alpha original",
        ["beta-note"] = "Beta original"
    };
    private int reads;
    private int writes;

    internal DocumentMetadata? FindMetadata(string id) => metadata.GetValueOrDefault(id);

    internal string ReadProtected(string id)
    {
        lock (gate) { reads++; return text[id]; }
    }

    internal string UpdateProtected(string id, string value)
    {
        lock (gate) { writes++; text[id] = value; return "Updated"; }
    }

    // Test observations only; not exposed as an HTTP endpoint or MCP tool.
    public StoreSnapshot Snapshot()
    {
        lock (gate) { return new(reads, writes, text["alpha-note"], text["beta-note"]); }
    }
}
