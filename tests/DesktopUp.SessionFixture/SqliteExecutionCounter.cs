using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;

// Runs only in the single-threaded diagnostic process. No SQL text, paths or bindings are retained.
// SQLite's STMT event counts executions, not unique SQL and not managed port invocations.
// Comment-prefixed STMT notifications are separate; do not assume they are all trigger executions.
// API semantics: https://sqlite.org/c3ref/auto_extension.html and https://sqlite.org/c3ref/c_trace.html
internal sealed class SqliteExecutionCounter : IDisposable
{
    private static SqliteExecutionCounter? active;
    private readonly OpenCallback opened;
    private readonly TraceCallback traced;
    private readonly nint openPointer;
    private readonly nint tracePointer;
    private readonly HashSet<nint> connections = [];
    private readonly Dictionary<string, int> statements = [];
    private readonly int thread = Environment.CurrentManagedThreadId;
    private int opens, commentEvents;
    private string? fault;
    private bool disposed;

    internal SqliteExecutionCounter()
    {
        if (Interlocked.CompareExchange(ref active, this, null) is not null)
            throw new InvalidOperationException("Only one fixture counter may be active.");
        opened = OnOpen;
        traced = OnTrace;
        openPointer = Marshal.GetFunctionPointerForDelegate(opened);
        tracePointer = Marshal.GetFunctionPointerForDelegate(traced);
        try
        {
            if (AutoExtension(openPointer) != 0) throw new IOException("Cannot register SQLite counter.");
        }
        catch { active = null; throw; }
    }

    internal void Attach(SqliteConnection connection) => Attach(connection.Handle!.DangerousGetHandle());

    private void Attach(nint database)
    {
        if (Trace(database, 1 | 8, tracePointer, 0) != 0) throw new IOException("Cannot attach SQLite counter.");
        connections.Add(database);
    }

    private int OnOpen(nint database, nint error, nint api)
    {
        // Never allow a managed exception to unwind through SQLite; fail the diagnostic afterwards.
        try { RequireThread(); Attach(database); opens++; }
        catch (Exception e) { fault ??= e.Message; }
        return 0;
    }

    private int OnTrace(uint kind, nint context, nint pointer, nint detail)
    {
        try
        {
            RequireThread();
            if (kind == 8) { connections.Remove(pointer); return 0; }
            string sql = (Marshal.PtrToStringUTF8(detail) ?? "").Trim();
            if (sql.StartsWith("--", StringComparison.Ordinal)) { commentEvents++; return 0; }
            string group = sql.StartsWith("PRAGMA quick_check", StringComparison.OrdinalIgnoreCase) ? "quick-check" :
                sql.Contains("FROM schema_migrations", StringComparison.OrdinalIgnoreCase) ? "migration-ledger" :
                sql.StartsWith("PRAGMA foreign_key_check", StringComparison.OrdinalIgnoreCase) ? "foreign-key-check" :
                sql.StartsWith("BEGIN", StringComparison.OrdinalIgnoreCase) ? "begin" :
                sql.StartsWith("COMMIT", StringComparison.OrdinalIgnoreCase) ? "commit" :
                sql.StartsWith("ROLLBACK", StringComparison.OrdinalIgnoreCase) ? "rollback" :
                sql.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase) ||
                sql.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase) ||
                sql.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase) ? "write" : "read-or-pragma";
            statements[group] = statements.GetValueOrDefault(group) + 1;
        }
        catch (Exception e) { fault ??= e.Message; }
        return 0;
    }

    private void RequireThread()
    {
        if (Environment.CurrentManagedThreadId != thread)
            throw new InvalidOperationException("Concurrent SQL is outside this synchronous fixture.");
    }

    internal void Reset() { opens = commentEvents = 0; statements.Clear(); }
    internal SqlCount Snapshot()
    {
        if (fault is not null) throw new IOException("SQL counting failed: " + fault);
        return new(opens, statements.Values.Sum(), commentEvents, new(statements));
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        // Remove only our registration and callbacks, never reset other native extensions.
        try
        {
            CancelExtension(openPointer);
            foreach (nint database in connections) Trace(database, 0, 0, 0);
            connections.Clear();
        }
        finally { active = null; GC.KeepAlive(opened); GC.KeepAlive(traced); }
    }

    internal sealed record SqlCount(int Connections, int StatementStarts, int CommentOrSubprogramEvents,
        Dictionary<string, int> Groups)
    {
        public int TraceEvents => StatementStarts + CommentOrSubprogramEvents;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int OpenCallback(nint database, nint error, nint api);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int TraceCallback(uint kind, nint context, nint pointer, nint detail);
    [DllImport("e_sqlite3", EntryPoint = "sqlite3_auto_extension", CallingConvention = CallingConvention.Cdecl)]
    private static extern int AutoExtension(nint callback);
    [DllImport("e_sqlite3", EntryPoint = "sqlite3_cancel_auto_extension", CallingConvention = CallingConvention.Cdecl)]
    private static extern int CancelExtension(nint callback);
    [DllImport("e_sqlite3", EntryPoint = "sqlite3_trace_v2", CallingConvention = CallingConvention.Cdecl)]
    private static extern int Trace(nint database, uint mask, nint callback, nint context);
}
