using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using IDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;

namespace DesktopUp.NativeDragProbe;

// HGLOBAL and IStream private-format storage for the Shell image helper. No CF_HDROP is offered
// by this source, so dropping its icon on Explorer cannot create/move user files.
[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
public sealed class ProbeDataObject : IDataObject, IDisposable
{
    public static short PayloadFormat => unchecked((short)RegisterClipboardFormat("DesktopUp.D1.ImageProbe.v1"));
    private const int FormatError = unchecked((int)0x80040064);
    private const int MediumError = unchecked((int)0x80040069);
    public const int MaximumBytes = 4 * 1024 * 1024;
    private readonly Dictionary<(short, int, DVASPECT), byte[]> values = [];
    private readonly Dictionary<(short, int, DVASPECT), STGMEDIUM> streams = [];
    private bool disposed;
    public Action<short, int>? FormatWritten { get; set; }
    public Action<short, TYMED, TYMED, long>? FormatOffered { get; set; }
    public ProbeDataObject(byte[] payload) => Put(PayloadFormat, payload);
    public void Put(short format, byte[] value)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (value.Length is 0 or > MaximumBytes) throw new ArgumentOutOfRangeException(nameof(value));
        values[(format, -1, DVASPECT.DVASPECT_CONTENT)] = value.ToArray();
    }
    public static FORMATETC Format(short id) => new() { cfFormat = id, dwAspect = DVASPECT.DVASPECT_CONTENT, lindex = -1, tymed = TYMED.TYMED_HGLOBAL };
    public int QueryGetData(ref FORMATETC format) => !disposed && format.ptd == 0 &&
        (((format.tymed & TYMED.TYMED_HGLOBAL) != 0 && values.ContainsKey((format.cfFormat, format.lindex, format.dwAspect))) ||
         ((format.tymed & TYMED.TYMED_ISTREAM) != 0 && streams.ContainsKey((format.cfFormat, format.lindex, format.dwAspect)))) ? 0 : FormatError;
    public void GetData(ref FORMATETC format, out STGMEDIUM medium)
    {
        int result = QueryGetData(ref format);
        if (result != 0) Marshal.ThrowExceptionForHR(result);
        var key = (format.cfFormat, format.lindex, format.dwAspect);
        if ((format.tymed & TYMED.TYMED_HGLOBAL) != 0 && values.TryGetValue(key, out var bytes)) medium = Allocate(bytes);
        else { var saved = streams[key]; Marshal.ThrowExceptionForHR(CopyStgMedium(ref saved, out medium)); }
    }
    public static STGMEDIUM Allocate(byte[] bytes)
    {
        nint handle = GlobalAlloc(0x42, (nuint)bytes.Length);
        if (handle == 0) throw new OutOfMemoryException();
        nint pointer = GlobalLock(handle);
        if (pointer == 0) { GlobalFree(handle); throw new OutOfMemoryException(); }
        try { Marshal.Copy(bytes, 0, pointer, bytes.Length); }
        finally { GlobalUnlock(handle); }
        return new() { tymed = TYMED.TYMED_HGLOBAL, unionmember = handle };
    }
    public void SetData(ref FORMATETC format, ref STGMEDIUM medium, bool release)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        FormatOffered?.Invoke(format.cfFormat, format.tymed, medium.tymed, medium.unionmember);
        var key = (format.cfFormat, format.lindex, format.dwAspect);
        if (format.ptd == 0 && (format.tymed & TYMED.TYMED_ISTREAM) != 0 && medium.tymed == TYMED.TYMED_ISTREAM && medium.unionmember != 0)
        {
            if (!streams.ContainsKey(key) && streams.Count >= 64) Marshal.ThrowExceptionForHR(FormatError);
            // Keep the stream and its release owner using COM's native medium-copy rules.
            // Do not flatten private streams or interpret them as virtual-file contents.
            Marshal.ThrowExceptionForHR(CopyStgMedium(ref medium, out var retained));
            if (streams.Remove(key, out var old)) ReleaseStgMedium(ref old);
            streams[key] = retained;
            if (release) ReleaseStgMedium(ref medium);
            FormatWritten?.Invoke(format.cfFormat, 0);
            return;
        }
        if (format.ptd != 0 || (format.tymed & TYMED.TYMED_HGLOBAL) == 0 || medium.tymed != TYMED.TYMED_HGLOBAL)
            Marshal.ThrowExceptionForHR(MediumError);
        var copy = CopyBytes(medium);
        if (!values.ContainsKey((format.cfFormat, format.lindex, format.dwAspect)) && values.Count >= 64)
            Marshal.ThrowExceptionForHR(FormatError);
        values[(format.cfFormat, format.lindex, format.dwAspect)] = copy;
        // On success only: fRelease transfers ownership. On failure the caller retains it.
        if (release) ReleaseStgMedium(ref medium);
        FormatWritten?.Invoke(format.cfFormat, copy.Length);
    }
    public static byte[] CopyBytes(STGMEDIUM medium)
    {
        if (medium.tymed != TYMED.TYMED_HGLOBAL || medium.unionmember == 0) Marshal.ThrowExceptionForHR(MediumError);
        nuint size = GlobalSize(medium.unionmember);
        if (size is 0 or > MaximumBytes) Marshal.ThrowExceptionForHR(MediumError);
        nint pointer = GlobalLock(medium.unionmember);
        if (pointer == 0) Marshal.ThrowExceptionForHR(MediumError);
        try { var bytes = new byte[(int)size]; Marshal.Copy(pointer, bytes, 0, bytes.Length); return bytes; }
        finally { GlobalUnlock(medium.unionmember); }
    }
    public static bool TryReadBytes(IDataObject data, short id, out byte[] bytes)
    {
        bytes = []; var format = Format(id);
        if (data.QueryGetData(ref format) != 0) return false;
        data.GetData(ref format, out var medium);
        try { bytes = CopyBytes(medium); return true; }
        finally { ReleaseStgMedium(ref medium); }
    }
    public IEnumFORMATETC EnumFormatEtc(DATADIR direction)
    {
        if (direction != DATADIR.DATADIR_GET) throw new COMException("GET only", unchecked((int)0x80004001));
        return new Formats(values.Keys.Select(key => new FORMATETC { cfFormat = key.Item1, lindex = key.Item2, dwAspect = key.Item3, tymed = TYMED.TYMED_HGLOBAL })
            .Concat(streams.Keys.Select(key => new FORMATETC { cfFormat = key.Item1, lindex = key.Item2, dwAspect = key.Item3, tymed = TYMED.TYMED_ISTREAM })).ToArray());
    }
    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    private sealed class Formats(FORMATETC[] entries) : IEnumFORMATETC
    {
        private int offset;
        public int Next(int count, FORMATETC[] output, int[]? fetched)
        {
            if (count < 0 || output.Length < count || (count != 1 && fetched is null)) return unchecked((int)0x80070057);
            int n = Math.Min(count, entries.Length - offset); Array.Copy(entries, offset, output, 0, n); offset += n;
            if (fetched is { Length: > 0 }) fetched[0] = n;
            return n == count ? 0 : 1;
        }
        public int Skip(int count) { if (count < 0) return unchecked((int)0x80070057); int n = Math.Min(count, entries.Length - offset); offset += n; return n == count ? 0 : 1; }
        public int Reset() { offset = 0; return 0; }
        public void Clone(out IEnumFORMATETC copy) => copy = new Formats(entries) { offset = offset };
    }
    public void GetDataHere(ref FORMATETC format, ref STGMEDIUM medium) => throw new COMException("Not supported", unchecked((int)0x80004001));
    public int GetCanonicalFormatEtc(ref FORMATETC input, out FORMATETC output) { output = input; output.ptd = 0; return 0x40130; }
    public int DAdvise(ref FORMATETC format, ADVF flags, IAdviseSink sink, out int connection) { connection = 0; return unchecked((int)0x80040003); }
    public void DUnadvise(int connection) { }
    public int EnumDAdvise(out IEnumSTATDATA? enumerator) { enumerator = null; return unchecked((int)0x80040003); }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; values.Clear();
        foreach (var item in streams.Values) { var medium = item; ReleaseStgMedium(ref medium); }
        streams.Clear();
    }
    [DllImport("urlmon.dll")] private static extern int CopyStgMedium(ref STGMEDIUM source, out STGMEDIUM destination);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern uint RegisterClipboardFormat(string name);
    [DllImport("ole32.dll")] public static extern void ReleaseStgMedium(ref STGMEDIUM medium);
    [DllImport("kernel32.dll")] private static extern nint GlobalAlloc(uint flags, nuint bytes);
    [DllImport("kernel32.dll")] private static extern nint GlobalLock(nint handle);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(nint handle);
    [DllImport("kernel32.dll")] private static extern nuint GlobalSize(nint handle);
    [DllImport("kernel32.dll")] private static extern nint GlobalFree(nint handle);
}
