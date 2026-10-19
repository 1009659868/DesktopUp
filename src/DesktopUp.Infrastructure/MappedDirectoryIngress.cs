using DesktopUp.Windows;
using System.Text.Json;
using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Infrastructure;

public sealed record MappedIngressIntent(Guid Id, string PosterId, MappedNewContent Target, IReadOnlyList<MappedIngressSource> Sources, bool Copy);

// Write-ahead intent and observed Shell results. Restart never replays a file operation.
public sealed class MappedDirectoryIngress(SqliteStateStore store, IShellDirectoryTransfer? executor = null) : IMappedIngressStorage
{
    internal static void Verify(SqliteConnection c, SqliteTransaction? t)
    { using var q=c.CreateCommand();q.Transaction=t;q.CommandText="SELECT operation_id,body_json,results_json,closed FROM mapped_directory_ingress LIMIT 0";using var r=q.ExecuteReader(); }
    internal static bool Pending(SqliteStateStore store) => store.ExecuteRead((c,t) =>
    { using var q=c.CreateCommand();q.Transaction=t;q.CommandText="SELECT EXISTS(SELECT 1 FROM mapped_directory_ingress WHERE closed=0)";return Convert.ToInt64(q.ExecuteScalar())!=0; });
    public int SettleInterrupted() => store.ExecuteWrite((c,t) =>
    { using var q=c.CreateCommand();q.Transaction=t;q.CommandText="UPDATE mapped_directory_ingress SET closed=1 WHERE closed=0";return q.ExecuteNonQuery(); });
    internal Action<string>? Checkpoint { get; set; }
    public Task<MappedIngressResult> Execute(string posterId, IReadOnlyList<string> paths, bool copy,
        IncomingContentPlacement? landing, nint owner, Func<bool> current, CancellationToken token = default,long? expectedPosterVersion = null) =>
        new MappedIngressUseCase(this, executor ?? new ShellDirectoryTransfer(), Checkpoint)
            .Execute(posterId, paths, copy, landing, owner, current, token, expectedPosterVersion);

    public IMappedIngressAttempt BeginAttempt() => new Attempt(store);
    private sealed class Attempt(SqliteStateStore store) : IMappedIngressAttempt
    {
        private readonly SqliteTransferJournal journal = new(store);
        private readonly MappedDirectoryService mapping = new(store);
        private readonly NativeShellExitJournal native = new(store);
        private readonly List<ShellExitIntent> exits = [];
        private Guid operation;
        private long targetVersion;
        private string targetPoster = "";
        private MappedNewContent? targetLocation;
        private IDisposable? targetPin;
        public IDisposable AcquireExecution() => new Execution(this, journal.AcquireExecutor());
        private sealed class Execution(Attempt attempt, IDisposable lease) : IDisposable
        {
            public void Dispose()
            {
                try { attempt.targetPin?.Dispose(); }
                finally { lease.Dispose(); }
            }
        }
        public MappedIngressPreparation Prepare(string posterId, IReadOnlyList<string> paths, bool copy,
            Func<bool> current, CancellationToken token, long? expectedPosterVersion)
        {
            int skipped=0;
            token.ThrowIfCancellationRequested();
            if(!current() || Pending(store) || journal.Read().Any(r=>!r.IsFinished) ||
                new SqliteStorageRelocationJournal(store).Read().Any(r=>r.Commit is null && r.Closure is null))throw new IOException("请等待当前文件操作结束。");
            var target=mapping.ResolveNewContent(posterId); targetPin=LocalTransferFiles.LockDirectory(target.Path);
            targetLocation=target; targetPoster=posterId;
            var before=new SqliteStateCommandRepository(store).Read();
            targetVersion=before.Catalog.Posters.Single(p=>p.Poster.PosterId==posterId).EntityVersion;
            if(expectedPosterVersion is { } expected && targetVersion!=expected)throw new IOException("松手后海报已变化，本次未移动。");
            if(before.Catalog.Posters.Single(p=>p.Poster.PosterId==posterId).Poster.IsLocked)throw new IOException("海报已锁定。");
            if(paths.Count is <1 or >100)throw new IOException("一次拖入最多100项。");
            var sources=paths.Select(LocalTransferFiles.Normalize).ToArray();
            if(sources.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=sources.Length || sources.Any(p=>sources.Any(q=>p!=q && LocalTransferFiles.Contains(p,q))))
                throw new IOException("不能同时拖入重复对象或父目录及其子项。");
            if(MappedDirectoryService.Scan(target.Path,token).Items.Count+sources.Length>ContentLayoutPolicy.MaximumItems)throw new IOException("目录映射最多1000个直接子项，未移动文件。");
            var facts=new List<MappedIngressSource>();
            foreach(string source in sources)
            {
                token.ThrowIfCancellationRequested();
                if(store.ProtectedTransferPath(source) || source.Equals(target.Path,StringComparison.OrdinalIgnoreCase) || LocalTransferFiles.Contains(source,target.Path) ||
                    LocalTransferFiles.Contains(target.Path,source) && !string.Equals(Path.GetDirectoryName(source),target.Path,StringComparison.OrdinalIgnoreCase))
                    throw new IOException("此路径受保护或与目标目录嵌套，未执行。");
                if(string.Equals(Path.GetDirectoryName(source),target.Path,StringComparison.OrdinalIgnoreCase)){skipped++;continue;}
                if(LocalContentReferenceProbe.Inspect(source).Item is null)throw new IOException("来源暂不可用："+source);
                ValidateTree(store,source,token);
                facts.Add(new(source,LocalTransferFiles.Identity(source)));
            }
            var intent=new MappedIngressIntent(Guid.NewGuid(),posterId,target,facts,copy);operation=intent.Id;
            store.ExecuteWrite((c,t)=>
            { using var q=c.CreateCommand();q.Transaction=t;q.CommandText="INSERT INTO mapped_directory_ingress(operation_id,body_json) VALUES($id,$body)";
                q.Parameters.AddWithValue("$id",operation.ToString());q.Parameters.AddWithValue("$body",JsonSerializer.Serialize(intent));return q.ExecuteNonQuery(); });
            if(!copy)
                foreach(var group in before.ContentCatalog.PosterItems.Where(p=>p.PosterId!=posterId && facts.Any(f=>
                    string.Equals(ContentRelationPolicy.Resolve(before.ContentCatalog,p.PosterId,p.LayoutItemId)!.Item.Target.Locator,f.Path,StringComparison.OrdinalIgnoreCase))).GroupBy(p=>p.PosterId))
                {var exit=native.Begin(group.Key,group.Select(p=>p.LayoutItemId).ToArray());native.End(exit,true);exits.Add(exit);}
            return new(target, facts, skipped);
        }
        public bool IsCurrent(MappedIngressSource source) =>
            LocalTransferFiles.Identity(targetLocation!.Path)==targetLocation.Identity &&
            LocalTransferFiles.Identity(source.Path)==source.Identity &&
            new SqliteStateCommandRepository(store).Read().Catalog.Posters.Any(p=>
                p.Poster.PosterId==targetPoster && p.EntityVersion==targetVersion && !p.Poster.IsLocked);
        public bool AcceptDestination(string path) =>
            string.Equals(Path.GetDirectoryName(path),targetLocation!.Path,StringComparison.OrdinalIgnoreCase) &&
            LocalTransferFiles.Identity(targetLocation.Path)==targetLocation.Identity &&
            LocalContentReferenceProbe.Inspect(path).Item is not null;
        public void RecordCompleted(IReadOnlyList<string> completed) => store.ExecuteWrite((c,t)=>
        {
            using var q=c.CreateCommand();q.Transaction=t;q.CommandText="UPDATE mapped_directory_ingress SET results_json=$body WHERE operation_id=$id";
            q.Parameters.AddWithValue("$id",operation.ToString());q.Parameters.AddWithValue("$body",JsonSerializer.Serialize(completed));return q.ExecuteNonQuery();
        });
        public void ObserveDepartures(ICollection<string> errors)
        {
            // The application has released the execution lease; these are only observations.
            foreach(var exit in exits)
                try{native.End(exit,true);native.ObserveReleasedMove(exit);native.Close(exit);}catch(Exception e){errors.Add("来源关联稍后核对："+e.Message);}
        }
        public bool HasPreparedOperation => operation!=Guid.Empty;
        public void PublishAndClose(IReadOnlyList<string> completed, IncomingContentPlacement? landing, ICollection<string> errors)
        {
            if(operation!=Guid.Empty)
            {
                try{using var lease=journal.AcquireObservationExecutor();mapping.CommitIncoming(targetPoster,completed,landing,targetVersion);}
                catch(Exception e){errors.Add("文件结果已保留，目录稍后同步："+e.Message);}
                store.ExecuteWrite((c,t)=>
                {using var q=c.CreateCommand();q.Transaction=t;q.CommandText="UPDATE mapped_directory_ingress SET closed=1 WHERE operation_id=$id";q.Parameters.AddWithValue("$id",operation.ToString());return q.ExecuteNonQuery();});
            }
        }
    }
    internal static void ValidateTree(SqliteStateStore store,string source,CancellationToken token)
    {
        // Do not let Shell follow a junction/cloud placeholder hidden inside an ordinary folder.
        // This is a read-only capability check, not a second copy or a hash of every file.
        var pending=new Stack<string>();pending.Push(source);int count=0;
        while(pending.TryPop(out var path))
        {
            token.ThrowIfCancellationRequested();
            if(++count>LocalTransferFiles.MaximumTreeEntries)throw new IOException("目录超过本批支持的4096项预算，未移动。");
            var attributes=File.GetAttributes(path);
            if(!LocalContentCompatibility.IsMaterialized(attributes) || store.ProtectedTransferPath(path))
                throw new IOException("目录包含受保护、重解析或离线对象，未移动。");
            if((attributes&FileAttributes.Directory)!=0)
                foreach(var child in Directory.EnumerateFileSystemEntries(path))
                {if(pending.Count+count>=LocalTransferFiles.MaximumTreeEntries)throw new IOException("目录超过本批支持的4096项预算，未移动。");pending.Push(child);}
        }
    }
}
