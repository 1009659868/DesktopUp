using DesktopUp.Application;

namespace DesktopUp.Infrastructure;

/// <summary>Bounded, local, data-only portable profile IO. Parsing and overwrite consent belong to the caller.</summary>
public sealed class PortableProfileFile
{
    public const int DefaultMaxBytes = 4 * 1024 * 1024;
    private readonly IUserDataPaths _paths;
    private readonly Action<string>? _fault;

    public PortableProfileFile(IUserDataPaths paths, Action<string>? fault = null)
    {
        _paths = paths;
        _fault = fault;
    }

    public byte[] Read(string path, int maxBytes = DefaultMaxBytes)
    {
        if (maxBytes is < 1 or > DefaultMaxBytes)
            throw new ArgumentOutOfRangeException(nameof(maxBytes));
        string full = ValidateProfilePath(path);
        using var stream = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > maxBytes)
            throw new InvalidDataException($"Portable profile exceeds {maxBytes} bytes.");
        byte[] bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1)
            throw new InvalidDataException("Portable profile changed while reading.");
        return bytes;
    }

    public void WriteAtomic(string path, ReadOnlyMemory<byte> bytes, bool overwriteConfirmed)
    {
        if (bytes.Length > DefaultMaxBytes)
            throw new InvalidDataException($"Portable profile exceeds {DefaultMaxBytes} bytes.");
        string full = ValidateProfilePath(path);
        if (LocalDataFileSafety.IsWithin(full, AppContext.BaseDirectory))
            throw new InvalidDataException("A portable export cannot overwrite application binaries or startup configuration.");
        if (!Directory.Exists(Path.GetDirectoryName(full)))
            throw new DirectoryNotFoundException("The selected destination directory does not exist.");
        bool existed = File.Exists(full);
        if (existed && !overwriteConfirmed)
            throw new IOException("Overwriting an existing file requires explicit confirmation.");
        string temporary = Path.Combine(Path.GetDirectoryName(full)!,
            $".desktopup-profile-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes.Span);
                stream.Flush(flushToDisk: true);
            }
            _fault?.Invoke("portable-file-before-replace");
            ValidateProfilePath(full);
            if (existed)
                File.Replace(temporary, full, null);
            else
                File.Move(temporary, full); // A concurrently created destination is never overwritten.
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private string ValidateProfilePath(string path)
    {
        string full = LocalDataFileSafety.Normalize(path);
        if (LocalDataFileSafety.IsWithin(full, _paths.State) ||
            LocalDataFileSafety.IsWithin(full, _paths.Backups) ||
            LocalDataFileSafety.IsWithin(full, _paths.Logs) ||
            LocalDataFileSafety.IsWithin(full, _paths.Temp))
            throw new InvalidDataException("Profile IO cannot target application state, backups, logs or temporary files.");
        string name = Path.GetFileName(full);
        if (name.EndsWith(".sqlite3", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".db", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith("-wal", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith("-shm", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("A portable profile must not replace or import a live database file.");
        return full;
    }
}

internal static class LocalDataFileSafety
{
    internal static bool IsWithin(string path, string directory)
    {
        string parent = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return path.Equals(parent, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    internal static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) ||
            path.StartsWith(@"\\", StringComparison.Ordinal) ||
            path.StartsWith("//", StringComparison.Ordinal))
            throw new InvalidDataException("Only fully qualified local filesystem paths are accepted.");
        // Windows normalization silently strips trailing dots/spaces; reject them before normalization.
        foreach (string segment in path[Path.GetPathRoot(path)!.Length..]
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            if (segment is not ("." or "..") && (segment.EndsWith(' ') || segment.EndsWith('.')))
                throw new InvalidDataException("Ambiguous path segments are not accepted.");
        string full = Path.GetFullPath(path);
        string root = Path.GetPathRoot(full)!;
        if (full[root.Length..].Contains(':'))
            throw new InvalidDataException("Alternate data streams are not accepted.");
        if (new DriveInfo(root).DriveType == DriveType.Network)
            throw new InvalidDataException("Network files are not accepted.");
        foreach (string segment in full[root.Length..].Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            string stem = segment.Split('.')[0].ToUpperInvariant();
            if (segment.EndsWith(' ') || segment.EndsWith('.') ||
                stem is "CON" or "PRN" or "AUX" or "NUL" ||
                (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) ||
                    stem.StartsWith("LPT", StringComparison.Ordinal)) && stem[3] is >= '0' and <= '9'))
                throw new InvalidDataException("Device names and ambiguous filenames are not accepted.");
        }
        for (string? current = full; current is not null; current = Path.GetDirectoryName(current))
        {
            if (!File.Exists(current) && !Directory.Exists(current)) continue;
            FileAttributes attributes = File.GetAttributes(current);
            if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Offline)) != 0)
                throw new InvalidDataException("Reparse points and offline files are not accepted.");
            if (current == full && (attributes & FileAttributes.Directory) != 0)
                throw new InvalidDataException("A regular file is required.");
        }
        return full;
    }
}
