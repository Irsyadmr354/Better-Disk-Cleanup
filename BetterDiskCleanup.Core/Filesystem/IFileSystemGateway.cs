namespace BetterDiskCleanup.Core.Filesystem;

public interface IFileSystemGateway
{
    bool DirectoryExists(string path);

    bool FileExists(string path);

    long GetFileSize(string path);

    DateTime GetLastWriteTimeUtc(string path);

    FileAttributes GetAttributes(string path);

    void ClearReadOnlyAttribute(string path);

    void CreateDirectory(string path);

    void MoveFile(string sourcePath, string destinationPath);

    void CopyFile(string sourcePath, string destinationPath, bool overwrite);

    string ComputeSha256Hash(string path);

    void DeleteFile(string path);

    void DeleteDirectory(string path, bool recursive);

    IEnumerable<string> EnumerateDirectories(string directoryPath);

    IEnumerable<string> EnumerateDirectoriesDirect(string directoryPath);

    IEnumerable<string> EnumerateFiles(string directoryPath);

    IEnumerable<string> EnumerateFilesDirect(string directoryPath);

    string ReadAllText(string path);
}
