using BetterDiskCleanup.Core.Filesystem;

namespace BetterDiskCleanup.Tests.Support;

public sealed class TrackingFileSystemGateway : IFileSystemGateway
{
    private readonly IFileSystemGateway _inner;

    public TrackingFileSystemGateway(IFileSystemGateway inner)
    {
        _inner = inner;
    }

    public int DeleteAttemptCount { get; private set; }

    public IReadOnlyCollection<string> DeletedFiles => _deletedFiles;

    private readonly HashSet<string> _deletedFiles = new(StringComparer.OrdinalIgnoreCase);

    public bool DirectoryExists(string path) => _inner.DirectoryExists(path);

    public bool FileExists(string path) => _inner.FileExists(path);

    public long GetFileSize(string path) => _inner.GetFileSize(path);

    public DateTime GetLastWriteTimeUtc(string path) => _inner.GetLastWriteTimeUtc(path);

    public FileAttributes GetAttributes(string path) => _inner.GetAttributes(path);

    public void ClearReadOnlyAttribute(string path) => _inner.ClearReadOnlyAttribute(path);

    public void CreateDirectory(string path) => _inner.CreateDirectory(path);

    public void MoveFile(string sourcePath, string destinationPath) => _inner.MoveFile(sourcePath, destinationPath);

    public void CopyFile(string sourcePath, string destinationPath, bool overwrite) =>
        _inner.CopyFile(sourcePath, destinationPath, overwrite);

    public string ComputeSha256Hash(string path) => _inner.ComputeSha256Hash(path);

    public void DeleteFile(string path)
    {
        DeleteAttemptCount++;
        _deletedFiles.Add(path);
        _inner.DeleteFile(path);
    }

    public void DeleteDirectory(string path, bool recursive) => _inner.DeleteDirectory(path, recursive);

    public IEnumerable<string> EnumerateDirectories(string directoryPath) =>
        _inner.EnumerateDirectories(directoryPath);

    public IEnumerable<string> EnumerateDirectoriesDirect(string directoryPath) =>
        _inner.EnumerateDirectoriesDirect(directoryPath);

    public IEnumerable<string> EnumerateFiles(string directoryPath) =>
        _inner.EnumerateFiles(directoryPath);

    public IEnumerable<string> EnumerateFilesDirect(string directoryPath) =>
        _inner.EnumerateFilesDirect(directoryPath);

    public string ReadAllText(string path) => _inner.ReadAllText(path);
}
