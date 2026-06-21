using System.IO;
using BetterDiskCleanup.Core.Filesystem;
using BetterDiskCleanup.Core.Safety;

namespace BetterDiskCleanup.Infrastructure.Safety;

public sealed class CriticalFileGuard : ICriticalFileGuard
{
    private readonly IFileLockInspector _fileLockInspector;

    private static readonly HashSet<string> CriticalFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "pagefile.sys",
        "hiberfil.sys",
        "swapfile.sys",
        "winre.wim",
        "bootmgr",
        "ntldr",
        "ntdetect.com"
    };

    private static readonly string[] CriticalDirectories =
    [
        "\\System Volume Information\\",
        "\\$Recycle.Bin\\"
    ];

    public CriticalFileGuard(IFileLockInspector fileLockInspector)
    {
        _fileLockInspector = fileLockInspector;
    }

    public CriticalFileCheckResult Check(string path)
    {
        var fileName = Path.GetFileName(path);
        if (CriticalFileNames.Contains(fileName))
        {
            return new CriticalFileCheckResult
            {
                IsCritical = true,
                Reason = "System critical file."
            };
        }

        foreach (var dir in CriticalDirectories)
        {
            if (path.Contains(dir, StringComparison.OrdinalIgnoreCase))
            {
                return new CriticalFileCheckResult
                {
                    IsCritical = true,
                    Reason = "System protected directory."
                };
            }
        }

        var extension = Path.GetExtension(path);
        if (string.Equals(extension, ".vhd", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".vhdx", StringComparison.OrdinalIgnoreCase))
        {
            var lockInfo = _fileLockInspector.TryGetLockingProcess(path);
            if (lockInfo is null)
            {
                // Let's also do a quick FileShare.None test just in case RestartManager doesn't catch it
                try
                {
                    using var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None);
                }
                catch (IOException)
                {
                    return new CriticalFileCheckResult
                    {
                        IsCritical = true,
                        Reason = "Active disk image (locked by system)."
                    };
                }
            }
            else
            {
                return new CriticalFileCheckResult
                {
                    IsCritical = true,
                    Reason = $"Active disk image (locked by {lockInfo.ProcessName})."
                };
            }
        }

        return new CriticalFileCheckResult
        {
            IsCritical = false,
            Reason = null
        };
    }
}
