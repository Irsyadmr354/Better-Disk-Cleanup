using BetterDiskCleanup.Core.Recovery;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BetterDiskCleanup.Tests.Support;

/// <summary>
/// Creates an isolated directory tree under %TEMP%\BetterDiskCleanup-tests\{prefix}-{guid}\
/// and deletes the entire tree on dispose, including recovery staging subfolders.
/// </summary>
public sealed class IsolatedTestRun : IDisposable
{
    public IsolatedTestRun(string prefix)
    {
        var runSegment = $"{prefix}-{Guid.NewGuid():N}";
        RootPath = Path.Combine(Path.GetTempPath(), "BetterDiskCleanup-tests", runSegment);
        DataDirectory = Path.Combine(RootPath, "data");
        StagingFolderName = Path.Combine("BetterDiskCleanup-tests", runSegment, "staging");

        Directory.CreateDirectory(DataDirectory);
    }

    public string RootPath { get; }

    public string DataDirectory { get; }

    /// <summary>
    /// Relative to %TEMP%; resolves to {RootPath}\staging\Recovery at runtime.
    /// </summary>
    public string StagingFolderName { get; }

    public ServiceProvider CreateServiceProvider()
    {
        return RecoveryTestServiceFactory.Create(StagingFolderName);
    }

    public IOptions<RecoveryOptions> CreateRecoveryOptions()
    {
        return Options.Create(new RecoveryOptions
        {
            StagingFolderName = StagingFolderName,
            RetentionDays = 30
        });
    }

    public string GetRecoveryRoot()
    {
        var userTempRoot = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Path.Combine(userTempRoot, StagingFolderName, "Recovery");
    }

    public void Dispose()
    {
        if (!Directory.Exists(RootPath))
        {
            return;
        }

        try
        {
            Directory.Delete(RootPath, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
