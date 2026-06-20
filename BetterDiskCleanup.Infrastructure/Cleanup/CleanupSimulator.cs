using BetterDiskCleanup.Core.Analysis;
using BetterDiskCleanup.Core.Cleanup;
using BetterDiskCleanup.Core.Filesystem;
using BetterDiskCleanup.Core.Safety;
using BetterDiskCleanup.Core.Recovery;
using BetterDiskCleanup.Infrastructure.Recovery;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BetterDiskCleanup.Infrastructure.Cleanup;

public sealed class CleanupSimulator : ICleanupSimulator
{
    private readonly IPathSafetyValidator _safetyValidator;
    private readonly IFileSystemGateway _fileSystem;
    private readonly IRecoveryOptions _recoveryOptions;
    private readonly ILogger<CleanupSimulator> _logger;

    public CleanupSimulator(
        IPathSafetyValidator safetyValidator,
        IFileSystemGateway fileSystem,
        IOptions<RecoveryOptions> recoveryOptions,
        ILogger<CleanupSimulator> logger)
    {
        _safetyValidator = safetyValidator;
        _fileSystem = fileSystem;
        _recoveryOptions = new RecoveryOptionsAdapter(recoveryOptions);
        _logger = logger;
    }

    public Task<CleanupSimulationResult> SimulateAsync(
        ScanResult scanResult,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => SimulateCore(scanResult, cancellationToken), cancellationToken);
    }

    private CleanupSimulationResult SimulateCore(ScanResult scanResult, CancellationToken cancellationToken)
    {
        long recoverableBytes = 0;
        var fileCount = 0;
        var skippedPaths = new List<string>();

        foreach (var item in scanResult.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (RecoveryPathHelper.IsUnderRecoveryStaging(item.Path, _recoveryOptions))
            {
                skippedPaths.Add(item.Path);
                continue;
            }

            var validation = _safetyValidator.Validate(item.Path);
            if (!validation.IsAllowed)
            {
                skippedPaths.Add(item.Path);
                continue;
            }

            if (!_fileSystem.FileExists(item.Path))
            {
                skippedPaths.Add(item.Path);
                continue;
            }

            try
            {
                recoverableBytes += _fileSystem.GetFileSize(item.Path);
                fileCount++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                skippedPaths.Add(item.Path);
                _logger.LogWarning(ex, "Simulation skipped inaccessible file: {Path}", item.Path);
            }
        }

        _logger.LogInformation(
            "Cleanup simulation completed. Recoverable files: {FileCount}, Bytes: {RecoverableBytes}",
            fileCount,
            recoverableBytes);

        return new CleanupSimulationResult
        {
            FileCount = fileCount,
            RecoverableBytes = recoverableBytes,
            SkippedPaths = skippedPaths
        };
    }
}
