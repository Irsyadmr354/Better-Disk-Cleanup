using BetterDiskCleanup.Core.Analysis;
using BetterDiskCleanup.Core.Cleanup;
using BetterDiskCleanup.Core.Filesystem;
using BetterDiskCleanup.Core.Recovery;
using BetterDiskCleanup.Core.Safety;
using BetterDiskCleanup.Infrastructure.Recovery;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BetterDiskCleanup.Infrastructure.Cleanup;

public sealed class CleanupExecutor : ICleanupExecutor
{
    private const int SharingViolationHResult = unchecked((int)0x80070020);

    private readonly IPathSafetyValidator _safetyValidator;
    private readonly IFileSystemGateway _fileSystem;
    private readonly IRecoverySnapshotService _recoverySnapshotService;
    private readonly IRecoveryOptions _recoveryOptions;
    private readonly ICleanupFailureDetailLogger _failureDetailLogger;
    private readonly IFileLockInspector _fileLockInspector;
    private readonly ICriticalFileGuard _criticalFileGuard;
    private readonly ILogger<CleanupExecutor> _logger;

    public CleanupExecutor(
        IPathSafetyValidator safetyValidator,
        IFileSystemGateway fileSystem,
        IRecoverySnapshotService recoverySnapshotService,
        IOptions<RecoveryOptions> recoveryOptions,
        ICleanupFailureDetailLogger failureDetailLogger,
        IFileLockInspector fileLockInspector,
        ICriticalFileGuard criticalFileGuard,
        ILogger<CleanupExecutor> logger)
    {
        _safetyValidator = safetyValidator;
        _fileSystem = fileSystem;
        _recoverySnapshotService = recoverySnapshotService;
        _recoveryOptions = new RecoveryOptionsAdapter(recoveryOptions);
        _failureDetailLogger = failureDetailLogger;
        _fileLockInspector = fileLockInspector;
        _criticalFileGuard = criticalFileGuard;
        _logger = logger;
    }

    public Task<CleanupReport> ExecuteAsync(
        ScanResult scanResult,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => ExecuteCore(scanResult, cancellationToken), cancellationToken);
    }

    private CleanupReport ExecuteCore(ScanResult scanResult, CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var warnings = new List<CleanupMessage>();
        var skippedInUse = new List<CleanupMessage>();
        var errors = new List<CleanupMessage>();
        var stagedItems = new List<RecoveryManifestItem>();
        var filesDeleted = 0;
        long spaceRecovered = 0;
        string? recoverySessionId = null;

        _failureDetailLogger.LogSessionStart(scanResult.Items.Count);

        _logger.LogInformation(
            "Cleanup session started. ItemCount={ItemCount}, RecoverySessionId={RecoverySessionId}, DetailLog={DetailLogPath}",
            scanResult.Items.Count,
            recoverySessionId,
            _failureDetailLogger.LogFilePath);

        foreach (var item in scanResult.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (RecoveryPathHelper.IsUnderRecoveryStaging(item.Path, _recoveryOptions))
            {
                warnings.Add(new CleanupMessage
                {
                    Path = item.Path,
                    Message = "Skipped because the path is inside the recovery staging area."
                });
                continue;
            }

            var validation = _safetyValidator.Validate(item.Path);
            if (!validation.IsAllowed)
            {
                var message = $"Skipped delete because safety validation failed: {validation.Reason}";
                warnings.Add(new CleanupMessage { Path = item.Path, Message = message });
                LogFailure(
                    CleanupFailureStage.SafetyRevalidation,
                    item.Path,
                    exception: null,
                    additionalContext: validation.Reason);
                continue;
            }

            var criticalCheck = _criticalFileGuard.Check(item.Path);
            if (criticalCheck.IsCritical)
            {
                var message = $"Skipped delete because file is critical: {criticalCheck.Reason}";
                warnings.Add(new CleanupMessage { Path = item.Path, Message = message });
                LogFailure(
                    CleanupFailureStage.SafetyRevalidation,
                    item.Path,
                    exception: null,
                    additionalContext: criticalCheck.Reason);
                continue;
            }

            if (!_fileSystem.FileExists(item.Path))
            {
                const string message = "File no longer exists at delete time.";
                warnings.Add(new CleanupMessage { Path = item.Path, Message = message });
                LogFailure(CleanupFailureStage.FileNotFound, item.Path, exception: null, message);
                continue;
            }

            long fileSize;
            try
            {
                fileSize = _fileSystem.GetFileSize(item.Path);
            }
            catch (Exception ex)
            {
                var message = $"Unable to read file size before delete: {ex.Message}";
                warnings.Add(new CleanupMessage { Path = item.Path, Message = message });
                LogFailure(CleanupFailureStage.SizeRead, item.Path, ex, message);
                continue;
            }

            if (!TryClearReadOnlyAfterSafetyCheck(item.Path, warnings, errors))
            {
                continue;
            }

            try
            {
                recoverySessionId ??= _recoverySnapshotService.BeginSession();
                var stageResult = _recoverySnapshotService.StageFile(recoverySessionId, item.Path);
                if (!stageResult.Success || stageResult.Item is null)
                {
                    var stageMessage = stageResult.ErrorMessage ?? "Unknown staging failure.";
                    if (stageMessage.Contains("being used by another process", StringComparison.OrdinalIgnoreCase))
                    {
                        var message = "Skipped because the file is in use by another process.";
                        var lockInfo = _fileLockInspector.TryGetLockingProcess(item.Path);
                        if (lockInfo is not null)
                        {
                            message += $" Locked by: {lockInfo.ProcessName} (PID: {lockInfo.ProcessId})";
                        }
                        
                        skippedInUse.Add(new CleanupMessage { Path = item.Path, Message = message });
                        LogFailure(CleanupFailureStage.FileInUse, item.Path, exception: null, message);
                        _logger.LogWarning("Skipped in-use file during staging: {Path}. {LockMsg}", item.Path, message);
                    }
                    else
                    {
                        errors.Add(new CleanupMessage
                        {
                            Path = item.Path,
                            Message = $"Recovery staging failed: {stageMessage}"
                        });
                        LogFailure(CleanupFailureStage.DeleteFile, item.Path, exception: null, stageMessage);
                        _logger.LogError("Failed to stage file for recovery: {Path}. Reason={Reason}", item.Path, stageMessage);
                    }

                    continue;
                }

                stagedItems.Add(stageResult.Item);
                filesDeleted++;
                spaceRecovered += fileSize;
            }
            catch (Exception ex) when (IsFileInUseException(ex))
            {
                var message = "Skipped because the file is in use by another process.";
                var lockInfo = _fileLockInspector.TryGetLockingProcess(item.Path);
                if (lockInfo is not null)
                {
                    message += $" Locked by: {lockInfo.ProcessName} (PID: {lockInfo.ProcessId})";
                }
                
                skippedInUse.Add(new CleanupMessage { Path = item.Path, Message = message });
                LogFailure(CleanupFailureStage.FileInUse, item.Path, ex, message);
                _logger.LogWarning(ex, "Skipped in-use file: {Path}. {LockMsg}", item.Path, message);
            }
            catch (Exception ex)
            {
                var message = $"Recovery staging failed: {ex.GetType().FullName}: {ex.Message}";
                errors.Add(new CleanupMessage { Path = item.Path, Message = message });
                LogFailure(CleanupFailureStage.DeleteFile, item.Path, ex, message);
                _logger.LogError(ex, "Failed to stage file for recovery: {Path}", item.Path);
            }
        }

        if (stagedItems.Count > 0 && recoverySessionId is not null)
        {
            _recoverySnapshotService.FinalizeSession(recoverySessionId, stagedItems);
        }

        var report = new CleanupReport
        {
            StartedAt = startedAt,
            CompletedAt = DateTimeOffset.UtcNow,
            FilesDeleted = filesDeleted,
            SpaceRecoveredBytes = spaceRecovered,
            Warnings = warnings,
            SkippedInUse = skippedInUse,
            Errors = errors,
            RecoverySessionId = stagedItems.Count > 0 ? recoverySessionId : null
        };

        _failureDetailLogger.LogSessionEnd(report);

        _logger.LogInformation(
            "Cleanup executed. FilesDeleted={FilesDeleted}, SpaceRecoveredBytes={SpaceRecoveredBytes}, " +
            "Warnings={WarningCount}, SkippedInUse={SkippedInUseCount}, Errors={ErrorCount}, RecoverySessionId={RecoverySessionId}, DetailLog={DetailLogPath}",
            report.FilesDeleted,
            report.SpaceRecoveredBytes,
            report.Warnings.Count,
            report.SkippedInUse.Count,
            report.Errors.Count,
            report.RecoverySessionId,
            _failureDetailLogger.LogFilePath);

        return report;
    }

    private bool TryClearReadOnlyAfterSafetyCheck(
        string path,
        List<CleanupMessage> warnings,
        List<CleanupMessage> errors)
    {
        FileAttributes attributes;
        try
        {
            attributes = _fileSystem.GetAttributes(path);
        }
        catch (Exception ex)
        {
            var message = $"Unable to read file attributes before delete: {ex.Message}";
            warnings.Add(new CleanupMessage { Path = path, Message = message });
            LogFailure(CleanupFailureStage.AttributeChange, path, ex, message);
            return false;
        }

        if (!attributes.HasFlag(FileAttributes.ReadOnly))
        {
            return true;
        }

        var validation = _safetyValidator.Validate(path);
        if (!validation.IsAllowed)
        {
            var message = $"Skipped clearing read-only attribute because safety validation failed: {validation.Reason}";
            warnings.Add(new CleanupMessage { Path = path, Message = message });
            LogFailure(
                CleanupFailureStage.SafetyRevalidation,
                path,
                exception: null,
                additionalContext: validation.Reason);
            return false;
        }

        try
        {
            _fileSystem.ClearReadOnlyAttribute(path);
            return true;
        }
        catch (Exception ex)
        {
            var message = $"Unable to clear read-only attribute before delete: {ex.Message}";
            errors.Add(new CleanupMessage { Path = path, Message = message });
            LogFailure(CleanupFailureStage.AttributeChange, path, ex, message);
            _logger.LogError(ex, "Failed to clear read-only attribute: {Path}", path);
            return false;
        }
    }

    internal static bool IsFileInUseException(Exception exception)
    {
        if (exception is IOException ioException)
        {
            if (ioException.HResult == SharingViolationHResult)
            {
                return true;
            }

            return ioException.Message.Contains("being used by another process", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private void LogFailure(
        CleanupFailureStage stage,
        string path,
        Exception? exception,
        string? additionalContext = null)
    {
        var detail = new CleanupFailureDetail
        {
            Stage = stage,
            Path = path,
            ExceptionType = exception?.GetType().FullName,
            ExceptionMessage = exception?.Message ?? additionalContext,
            HResult = exception?.HResult,
            AdditionalContext = additionalContext
        };

        _failureDetailLogger.LogFailure(detail);
    }
}
