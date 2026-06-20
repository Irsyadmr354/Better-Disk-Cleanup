using BetterDiskCleanup.Core.Filesystem;
using BetterDiskCleanup.Core.LargeFiles;
using BetterDiskCleanup.Infrastructure.LargeFiles;
using BetterDiskCleanup.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace BetterDiskCleanup.Tests.LargeFiles;

public sealed class LargeFileScannerAccessDeniedTests
{
    [Fact]
    public async Task Scan_AccessDeniedSubfolder_DoesNotCrash()
    {
        // Arrange
        var fs = new InMemoryFileSystemGateway();
        var root = @"C:\TestData";
        var protectedDir = Path.Combine(root, "SystemVolumeInformation");
        fs.AddDirectory(root);
        fs.AddDirectory(protectedDir);

        var threshold = 100L * 1024 * 1024;
        fs.AddFile(Path.Combine(root, "big_file.mp4"), threshold, content: [1, 2, 3]);
        fs.AddFile(Path.Combine(protectedDir, "hidden.sys"), threshold * 2, content: [1, 2, 3]);

        var restrictedFs = new AccessDeniedFileSystemGateway(fs, [protectedDir]);
        var scanner = new LargeFileScanner(restrictedFs, NullLogger<LargeFileScanner>.Instance);

        // Act
        var result = await scanner.ScanAsync(root, threshold);

        // Assert — should not crash, should have warning
        Assert.Single(result.Entries); // only the root file
        Assert.Contains(result.Entries, e => e.FileName == "big_file.mp4");
        Assert.NotEmpty(result.Warnings);
        Assert.Contains(result.Warnings, w => w.Path == protectedDir);
    }

    [Fact]
    public async Task Scan_MultipleAccessDeniedFolders_RecordsAllWarnings()
    {
        // Arrange
        var fs = new InMemoryFileSystemGateway();
        var root = @"C:\TestData";
        var protected1 = Path.Combine(root, "protected1");
        var protected2 = Path.Combine(root, "protected2");
        var accessible = Path.Combine(root, "accessible");
        fs.AddDirectory(root);
        fs.AddDirectory(protected1);
        fs.AddDirectory(protected2);
        fs.AddDirectory(accessible);

        var threshold = 100L * 1024 * 1024;
        fs.AddFile(Path.Combine(accessible, "data.zip"), threshold, content: [1, 2, 3]);

        var restrictedFs = new AccessDeniedFileSystemGateway(fs, [protected1, protected2]);
        var scanner = new LargeFileScanner(restrictedFs, NullLogger<LargeFileScanner>.Instance);

        // Act
        var result = await scanner.ScanAsync(root, threshold);

        // Assert
        Assert.Single(result.Entries);
        Assert.Equal(2, result.Warnings.Count(w =>
            w.Path == protected1 || w.Path == protected2));
    }

    /// <summary>
    /// Wrapper that throws UnauthorizedAccessException on EnumerateFilesDirect
    /// for specified restricted paths.
    /// </summary>
    private sealed class AccessDeniedFileSystemGateway : IFileSystemGateway
    {
        private readonly IFileSystemGateway _inner;
        private readonly HashSet<string> _restrictedPaths;

        public AccessDeniedFileSystemGateway(IFileSystemGateway inner, IEnumerable<string> restrictedPaths)
        {
            _inner = inner;
            _restrictedPaths = new HashSet<string>(restrictedPaths, StringComparer.OrdinalIgnoreCase);
        }

        private void ThrowIfRestricted(string path)
        {
            var normalized = Path.GetFullPath(path);
            if (_restrictedPaths.Any(r => normalized.StartsWith(Path.GetFullPath(r), StringComparison.OrdinalIgnoreCase)))
            {
                throw new UnauthorizedAccessException($"Access denied: {path}");
            }
        }

        public bool DirectoryExists(string path) => _inner.DirectoryExists(path);
        public bool FileExists(string path) => _inner.FileExists(path);
        public long GetFileSize(string path) { ThrowIfRestricted(path); return _inner.GetFileSize(path); }
        public DateTime GetLastWriteTimeUtc(string path) => _inner.GetLastWriteTimeUtc(path);
        public FileAttributes GetAttributes(string path) => _inner.GetAttributes(path);
        public void ClearReadOnlyAttribute(string path) => _inner.ClearReadOnlyAttribute(path);
        public void CreateDirectory(string path) => _inner.CreateDirectory(path);
        public void MoveFile(string sourcePath, string destinationPath) => _inner.MoveFile(sourcePath, destinationPath);
        public void CopyFile(string sourcePath, string destinationPath, bool overwrite) => _inner.CopyFile(sourcePath, destinationPath, overwrite);
        public string ComputeSha256Hash(string path) => _inner.ComputeSha256Hash(path);
        public void DeleteFile(string path) => _inner.DeleteFile(path);
        public void DeleteDirectory(string path, bool recursive) => _inner.DeleteDirectory(path, recursive);
        public IEnumerable<string> EnumerateDirectories(string directoryPath) => _inner.EnumerateDirectories(directoryPath);
        public IEnumerable<string> EnumerateDirectoriesDirect(string directoryPath) => _inner.EnumerateDirectoriesDirect(directoryPath);
        public IEnumerable<string> EnumerateFiles(string directoryPath) => _inner.EnumerateFiles(directoryPath);

        public IEnumerable<string> EnumerateFilesDirect(string directoryPath)
        {
            ThrowIfRestricted(directoryPath);
            return _inner.EnumerateFilesDirect(directoryPath);
        }

        public string ReadAllText(string path) => _inner.ReadAllText(path);
    }
}
