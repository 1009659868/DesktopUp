using DesktopUp.Application;

namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime
{
    private readonly Dictionary<string,MappedDirectoryState> _mappingStates=new(StringComparer.Ordinal);
    private readonly Dictionary<string,string> _mappingMessages=new(StringComparer.Ordinal);
    internal bool MappingAvailable(string id)=>ContentActionPath(id,null) is null || !_mappingStates.TryGetValue(id,out var state) || state.Available;
    internal MappedDirectoryState? MappingState(string id)=>_mappingStates.GetValueOrDefault(id);
    internal string? MappingMessage(string id)=>_mappingMessages.GetValueOrDefault(id);
    internal void PublishMappingStates(IReadOnlyList<MappedDirectoryState> states)
    {
        foreach(var state in states)
        {
            if(_mappingStates.GetValueOrDefault(state.PosterId)==state)continue;
            bool restored=_mappingStates.TryGetValue(state.PosterId,out var old) && !old.Available && state.Available;
            _mappingStates[state.PosterId]=state;
            if(restored)_mappingMessages.Remove(state.PosterId);
            if(_windows.TryGetValue(state.PosterId,out var window))window.UpdateMappingBadge(restored);
        }
        if(TryGetProjection(out var projection) && projection is not null)
            foreach(string id in _mappingStates.Keys.Where(id=>!projection.PostersById.ContainsKey(id)).ToArray())
            {_mappingStates.Remove(id);_mappingMessages.Remove(id);}
    }
    internal void SetMappingOperationMessage(string id,string? message)
    {
        if(message is null)_mappingMessages.Remove(id);else _mappingMessages[id]=message;
        if(_windows.TryGetValue(id,out var window))window.UpdateMappingBadge();
    }
}
