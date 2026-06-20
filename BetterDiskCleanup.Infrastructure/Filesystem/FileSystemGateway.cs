using System.Security.Cryptography;
using BetterDiskCleanup.Core.Filesystem;

namespace BetterDiskCleanup.Infrastructure.Filesystem;

public sealed class FileSystemGateway : IFileSystemGateway
{
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public bool FileExists(string path) => File.Exists(path);

    public long GetFileSize(string path) => new FileInfo(path).Length;

    public DateTime GetLastWriteTimeUtc(string path) => File.GetLastWriteTimeUtc(path);

    public FileAttributes GetAttributes(string path) => File.GetAttributes(path);

    public void ClearReadOnlyAttribute(string path)
    {
        var attributes = File.GetAttributes(path);
        if (attributes.HasFlag(FileAttributes.ReadOnly))
        {
            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
        }
    }

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public void MoveFile(string sourcePath, string destinationPath) => File.Move(sourcePath, destinationPath);

    public void CopyFile(string sourcePath, string destinationPath, bool overwrite) =>
        File.Copy(sourcePath, destinationPath, overwrite);

    public string ComputeSha256Hash(string path)
    {
        using var stream = File.OpenRead(path);
        var hashBytes = SHA256.HashData(stream);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public void DeleteFile(string path) => File.Delete(path);

    public void DeleteDirectory(string path, bool recursive) => Directory.Delete(path, recursive);

    public IEnumerable<string> EnumerateDirectories(string directoryPath) =>
        EnumerateDirectoriesRecursive(directoryPath);

    public IEnumerable<string> EnumerateDirectoriesDirect(string directoryPath) =>
        Directory.EnumerateDirectories(directoryPath);

    public IEnumerable<string> EnumerateFiles(string directoryPath) =>
        EnumerateFilesRecursive(directoryPath);

    public IEnumerable<string> EnumerateFilesDirect(string directoryPath)
    {
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(directoryPath);
        }
        catch (UnauthorizedAccessException)
        {
            yield break;
        }
        catch (DirectoryNotFoundException)
        {
            yield break;
        }

        foreach (var file in files)
        {
            yield return file;
        }
    }

    public string ReadAllText(string path) => File.ReadAllText(path);

    private static IEnumerable<string> EnumerateDirectoriesRecursive(string rootPath)
    {
        var pending = new Stack<string>();
        pending.Push(rootPath);

        while (pending.Count > 0)
        {
            var current = pending.Pop();

            IEnumerable<string> childDirectories;
            try
            {
                childDirectories = Directory.EnumerateDirectories(current);
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (DirectoryNotFoundException)
            {
                continue;
            }

            foreach (var childDirectory in childDirectories)
            {
                if (ShouldSkipDirectoryTraversal(childDirectory))
                {
                    continue;
                }

                yield return childDirectory;
                pending.Push(childDirectory);
            }
        }
    }

    private static IEnumerable<string> EnumerateFilesRecursive(string rootPath)
    {
        var pending = new Stack<string>();
        pending.Push(rootPath);

        while (pending.Count > 0)
        {
            var current = pending.Pop();

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(current);
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (DirectoryNotFoundException)
            {
                continue;
            }

            foreach (var file in files)
            {
                yield return file;
            }

            IEnumerable<string> childDirectories;
            try
            {
                childDirectories = Directory.EnumerateDirectories(current);
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (DirectoryNotFoundException)
            {
                continue;
            }

            foreach (var childDirectory in childDirectories)
            {
                if (ShouldSkipDirectoryTraversal(childDirectory))
                {
                    continue;
                }

                pending.Push(childDirectory);
            }
        }
    }

    private static bool ShouldSkipDirectoryTraversal(string directoryPath)
    {
        try
        {
            return (File.GetAttributes(directoryPath) & FileAttributes.ReparsePoint) != 0;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
        catch (IOException)
        {
            return true;
        }
    }
}
