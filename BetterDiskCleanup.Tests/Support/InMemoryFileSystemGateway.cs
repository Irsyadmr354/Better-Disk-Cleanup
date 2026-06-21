using BetterDiskCleanup.Core.Filesystem;

namespace BetterDiskCleanup.Tests.Support;

public sealed class InMemoryFileSystemGateway : IFileSystemGateway
{
    private readonly Dictionary<string, FileEntry> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _deletedFiles = [];

    public IReadOnlyList<string> DeletedFiles => _deletedFiles;

    public void AddDirectory(string path)
    {
        _directories.Add(Normalize(path));
    }

    public void AddFile(
        string path,
        long sizeBytes,
        DateTime? lastModifiedUtc = null,
        FileAttributes attributes = FileAttributes.Normal,
        byte[]? content = null,
        DateTime? createdUtc = null)
    {
        var normalized = Normalize(path);
        var directory = Path.GetDirectoryName(normalized);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            _directories.Add(directory!);
        }

        _files[normalized] = new FileEntry(
            sizeBytes,
            lastModifiedUtc ?? DateTime.UtcNow,
            attributes,
            content ?? CreateContent(sizeBytes),
            createdUtc ?? lastModifiedUtc ?? DateTime.UtcNow);
    }

    public bool DirectoryExists(string path) => _directories.Contains(Normalize(path));

    public bool FileExists(string path) => _files.ContainsKey(Normalize(path));

    public long GetFileSize(string path) => _files[Normalize(path)].SizeBytes;

    public DateTime GetLastWriteTimeUtc(string path) => _files[Normalize(path)].LastModifiedUtc;

    public DateTime GetCreationTimeUtc(string path) => _files[Normalize(path)].CreatedUtc;

    public FileAttributes GetAttributes(string path) => _files[Normalize(path)].Attributes;

    public void ClearReadOnlyAttribute(string path)
    {
        var normalized = Normalize(path);
        var entry = _files[normalized];
        _files[normalized] = entry with
        {
            Attributes = entry.Attributes & ~FileAttributes.ReadOnly
        };
    }

    public void CreateDirectory(string path)
    {
        _directories.Add(Normalize(path));
    }

    public void MoveFile(string sourcePath, string destinationPath)
    {
        var source = Normalize(sourcePath);
        var destination = Normalize(destinationPath);
        var entry = _files[source];
        var destinationDirectory = Path.GetDirectoryName(destination);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            _directories.Add(destinationDirectory);
        }

        _files[destination] = entry;
        _files.Remove(source);
    }

    public void CopyFile(string sourcePath, string destinationPath, bool overwrite)
    {
        var source = Normalize(sourcePath);
        var destination = Normalize(destinationPath);
        if (!overwrite && _files.ContainsKey(destination))
        {
            throw new IOException("Destination file already exists.");
        }

        var destinationDirectory = Path.GetDirectoryName(destination);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            _directories.Add(destinationDirectory);
        }

        var entry = _files[source];
        _files[destination] = entry with { Content = entry.Content.ToArray() };
    }

    public string ComputeSha256Hash(string path)
    {
        var entry = _files[Normalize(path)];
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(entry.Content)).ToLowerInvariant();
    }

    public void DeleteFile(string path)
    {
        var normalized = Normalize(path);
        _deletedFiles.Add(normalized);
        _files.Remove(normalized);
    }

    public void DeleteDirectory(string path, bool recursive)
    {
        var normalized = Normalize(path);
        _directories.Remove(normalized);

        if (recursive)
        {
            var prefix = normalized + Path.DirectorySeparatorChar;
            foreach (var file in _files.Keys.Where(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                _files.Remove(file);
            }

            foreach (var directory in _directories.Where(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                _directories.Remove(directory);
            }
        }
    }

    public IEnumerable<string> EnumerateDirectories(string directoryPath)
    {
        var root = Normalize(directoryPath);
        return _directories
            .Where(directory => directory.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                && !directory.Equals(root, StringComparison.OrdinalIgnoreCase));
    }

    public IEnumerable<string> EnumerateDirectoriesDirect(string directoryPath)
    {
        var root = Normalize(directoryPath);
        var rootWithSep = root + Path.DirectorySeparatorChar;
        return _directories
            .Where(d => d.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase)
                && !d.Substring(rootWithSep.Length).Contains(Path.DirectorySeparatorChar));
    }

    public IEnumerable<string> EnumerateFiles(string directoryPath)
    {
        var root = Normalize(directoryPath);
        return _files.Keys
            .Where(file => Path.GetDirectoryName(file)?.StartsWith(root, StringComparison.OrdinalIgnoreCase) == true
                || file.StartsWith(root, StringComparison.OrdinalIgnoreCase));
    }

    public IEnumerable<string> EnumerateFilesDirect(string directoryPath)
    {
        var root = Normalize(directoryPath);
        return _files.Keys
            .Where(file => string.Equals(
                Path.GetDirectoryName(file),
                root,
                StringComparison.OrdinalIgnoreCase));
    }

    public string ReadAllText(string path)
    {
        var entry = _files[Normalize(path)];
        return System.Text.Encoding.UTF8.GetString(entry.Content);
    }

    private static string Normalize(string path) => Path.GetFullPath(path);

    private static byte[] CreateContent(long sizeBytes)
    {
        var buffer = new byte[sizeBytes];
        for (var index = 0; index < buffer.Length; index++)
        {
            buffer[index] = (byte)(index % 251);
        }

        return buffer;
    }

    private sealed record FileEntry(
        long SizeBytes,
        DateTime LastModifiedUtc,
        FileAttributes Attributes,
        byte[] Content,
        DateTime CreatedUtc);
}
