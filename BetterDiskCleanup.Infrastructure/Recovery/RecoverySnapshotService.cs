using BetterDiskCleanup.Core.Filesystem;
using BetterDiskCleanup.Core.Recovery;
using BetterDiskCleanup.Core.Safety;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BetterDiskCleanup.Infrastructure.Recovery;

public sealed class RecoverySnapshotService : IRecoverySnapshotService
{
    private readonly IFileSystemGateway _fileSystem;
    private readonly IPathSafetyValidator _safetyValidator;
    private readonly IRecoveryOptions _options;
    private readonly RecoveryManifestStore _manifestStore;
    private readonly ILogger<RecoverySnapshotService> _logger;
    private readonly Dictionary<string, DateTimeOffset> _sessionCreatedAt = new();

    public RecoverySnapshotService(
        IFileSystemGateway fileSystem,
        IPathSafetyValidator safetyValidator,
        IOptions<RecoveryOptions> options,
        ILogger<RecoverySnapshotService> logger)
    {
        _fileSystem = fileSystem;
        _safetyValidator = safetyValidator;
        _options = new RecoveryOptionsAdapter(options);
        _manifestStore = new RecoveryManifestStore();
        _logger = logger;

        EnsureRecoveryRootReady();
    }

    public string BeginSession()
    {
        EnsureRecoveryRootReady();
        var sessionId = Guid.NewGuid().ToString("N");
        var sessionDirectory = RecoveryPathHelper.GetSessionDirectory(_options, sessionId);
        _fileSystem.CreateDirectory(Path.Combine(sessionDirectory, "files"));
        _sessionCreatedAt[sessionId] = DateTimeOffset.UtcNow;
        _logger.LogInformation("Recovery session started: {SessionId}", sessionId);
        return sessionId;
    }

    public RecoveryStageResult StageFile(string sessionId, string originalPath)
    {
        var validation = _safetyValidator.Validate(originalPath);
        if (!validation.IsAllowed)
        {
            return RecoveryStageResult.Failed(
                $"Cannot stage file because safety validation failed: {validation.Reason}");
        }

        if (!_fileSystem.FileExists(originalPath))
        {
            return RecoveryStageResult.Failed("Original file no longer exists.");
        }

        var sessionDirectory = RecoveryPathHelper.GetSessionDirectory(_options, sessionId);
        var stagedValidation = _safetyValidator.Validate(sessionDirectory);
        if (!stagedValidation.IsAllowed)
        {
            return RecoveryStageResult.Failed(
                $"Recovery session directory failed safety validation: {stagedValidation.Reason}");
        }

        var itemId = Guid.NewGuid().ToString("N");
        var stagedPath = RecoveryPathHelper.GetStagedFilePath(sessionDirectory, itemId);

        try
        {
            if (_fileSystem.GetAttributes(originalPath).HasFlag(FileAttributes.ReadOnly))
            {
                var readOnlyValidation = _safetyValidator.Validate(originalPath);
                if (!readOnlyValidation.IsAllowed)
                {
                    return RecoveryStageResult.Failed(
                        $"Cannot clear read-only attribute because safety validation failed: {readOnlyValidation.Reason}");
                }

                _fileSystem.ClearReadOnlyAttribute(originalPath);
            }

            var hash = _fileSystem.ComputeSha256Hash(originalPath);
            var size = _fileSystem.GetFileSize(originalPath);
            _fileSystem.MoveFile(originalPath, stagedPath);

            var stagedHash = _fileSystem.ComputeSha256Hash(stagedPath);
            if (!string.Equals(hash, stagedHash, StringComparison.OrdinalIgnoreCase))
            {
                return RecoveryStageResult.Failed("Staged file hash does not match source hash.");
            }

            var item = new RecoveryManifestItem
            {
                ItemId = itemId,
                OriginalPath = Path.GetFullPath(originalPath),
                StagedPath = stagedPath,
                SizeBytes = size,
                StagedAtUtc = DateTimeOffset.UtcNow,
                Sha256Hash = hash,
                Status = RecoveryItemStatus.Staged
            };

            return RecoveryStageResult.Succeeded(item);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return RecoveryStageResult.Failed($"{ex.GetType().Name}: {ex.Message}");
        }
    }

    public void FinalizeSession(string sessionId, IReadOnlyList<RecoveryManifestItem> stagedItems)
    {
        if (!_sessionCreatedAt.TryGetValue(sessionId, out var createdAt))
        {
            createdAt = DateTimeOffset.UtcNow;
        }

        var manifest = new RecoveryManifest
        {
            SessionId = sessionId,
            CreatedAtUtc = createdAt,
            ExpiresAtUtc = createdAt.AddDays(_options.RetentionDays),
            Status = RecoverySessionStatus.Active,
            Items = stagedItems.ToList()
        };

        var manifestPath = RecoveryPathHelper.GetManifestPath(_options, sessionId);
        _manifestStore.Save(manifestPath, manifest);
        _sessionCreatedAt.Remove(sessionId);

        _logger.LogInformation(
            "Recovery session finalized: {SessionId}, Items={ItemCount}, ExpiresAt={ExpiresAt}",
            sessionId,
            stagedItems.Count,
            manifest.ExpiresAtUtc);
    }

    public string GetRecoveryRootPath()
    {
        EnsureRecoveryRootReady();
        return RecoveryPathHelper.GetRecoveryRoot(_options);
    }

    private void EnsureRecoveryRootReady()
    {
        var recoveryRoot = RecoveryPathHelper.GetRecoveryRoot(_options);
        RecoveryPathHelper.EnsureRecoveryRootIsSafe(_safetyValidator, recoveryRoot);
        _fileSystem.CreateDirectory(recoveryRoot);
    }
}
