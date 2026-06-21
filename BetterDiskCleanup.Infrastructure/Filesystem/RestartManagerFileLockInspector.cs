using System.Diagnostics;
using System.Runtime.InteropServices;
using BetterDiskCleanup.Core.Filesystem;
using Microsoft.Extensions.Logging;

namespace BetterDiskCleanup.Infrastructure.Filesystem;

public sealed class RestartManagerFileLockInspector : IFileLockInspector
{
    private const int RmRebootReasonNone = 0;
    private const int CchRmMaxAppName = 255;
    private const int CchRmMaxSvcName = 63;

    [StructLayout(LayoutKind.Sequential)]
    private struct RM_UNIQUE_PROCESS
    {
        public int dwProcessId;
        public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RM_PROCESS_INFO
    {
        public RM_UNIQUE_PROCESS Process;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchRmMaxAppName + 1)]
        public string strAppName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchRmMaxSvcName + 1)]
        public string strServiceShortName;
        public int ApplicationType;
        public uint AppStatus;
        public uint TSSessionId;
        [MarshalAs(UnmanagedType.Bool)]
        public bool bRestartable;
    }

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, string strSessionKey);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmEndSession(uint pSessionHandle);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int RmRegisterResources(
        uint pSessionHandle,
        uint nFiles,
        string[] rgsFilenames,
        uint nApplications,
        [In] RM_UNIQUE_PROCESS[]? rgApplications,
        uint nServices,
        string[]? rgsServiceNames);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int RmGetList(
        uint dwSessionHandle,
        out uint pnProcInfoNeeded,
        ref uint pnProcInfo,
        [In, Out] RM_PROCESS_INFO[] rgAffectedApps,
        out uint lpdwRebootReasons);

    private readonly ILogger<RestartManagerFileLockInspector> _logger;

    public RestartManagerFileLockInspector(ILogger<RestartManagerFileLockInspector> logger)
    {
        _logger = logger;
    }

    public FileLockInfo? TryGetLockingProcess(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        uint sessionHandle = 0;
        try
        {
            int res = RmStartSession(out sessionHandle, 0, Guid.NewGuid().ToString());
            if (res != 0)
            {
                return null;
            }

            string[] resources = [path];
            res = RmRegisterResources(sessionHandle, (uint)resources.Length, resources, 0, null, 0, null);
            if (res != 0)
            {
                return null;
            }

            uint pnProcInfoNeeded = 0;
            uint pnProcInfo = 0;
            uint lpdwRebootReasons = RmRebootReasonNone;

            res = RmGetList(sessionHandle, out pnProcInfoNeeded, ref pnProcInfo, [], out lpdwRebootReasons);
            
            if (res == 234) // ERROR_MORE_DATA
            {
                pnProcInfo = pnProcInfoNeeded;
                var processInfo = new RM_PROCESS_INFO[pnProcInfo];
                res = RmGetList(sessionHandle, out pnProcInfoNeeded, ref pnProcInfo, processInfo, out lpdwRebootReasons);

                if (res == 0 && pnProcInfo > 0)
                {
                    // Return the first process locking the file
                    var lockProc = processInfo[0];
                    var processId = lockProc.Process.dwProcessId;
                    var processName = lockProc.strAppName;

                    // Fallback to getting name from Process API if strAppName is empty
                    if (string.IsNullOrWhiteSpace(processName))
                    {
                        try
                        {
                            using var process = Process.GetProcessById(processId);
                            processName = process.ProcessName;
                        }
                        catch
                        {
                            processName = "Unknown Process";
                        }
                    }

                    return new FileLockInfo
                    {
                        ProcessName = processName,
                        ProcessId = processId
                    };
                }
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get locking process for path {Path}", path);
            return null;
        }
        finally
        {
            if (sessionHandle != 0)
            {
                RmEndSession(sessionHandle);
            }
        }
    }
}
