using BetterDiskCleanup.Core.Filesystem;
using BetterDiskCleanup.Core.Recovery;
using BetterDiskCleanup.Core.Safety;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BetterDiskCleanup.Infrastructure.Recovery;

public sealed class RecoveryService : IRecoveryService
{
    private readonly IFileSystemGateway _fileSystem;
    private readonly IPathSafetyValidator _safetyValidator;
    private readonly IRecoveryOptions _options;
    private readonly RecoveryManifestStore _manifestStore;
    private readonly ILogger<RecoveryService> _logger;

    public RecoveryService(
        IFileSystemGateway fileSystem,
        IPathSafetyValidator safetyValidator,
        IOptions<RecoveryOptions> options,
        ILogger<RecoveryService> logger)
    {
        _fileSystem = fileSystem;
        _safetyValidator = safetyValidator;
        _options = new RecoveryOptionsAdapter(options);
        _manifestStore = new RecoveryManifestStore();
        _logger = logger;
    }

    public IReadOnlyList<RecoverySessionSummary> ListSessions()
    {
        var recoveryRoot = RecoveryPathHelper.GetRecoveryRoot(_options);
        if (!_fileSystem.DirectoryExists(recoveryRoot))
        {
            return [];
        }

        var summaries = new List<RecoverySessionSummary>();
        foreach (var sessionDirectory in Directory.EnumerateDirectories(recoveryRoot))
        {
            var sessionId = Path.GetFileName(sessionDirectory);
            var manifest = LoadManifest(sessionId);
            if (manifest is null)
            {
                continue;
            }

            summaries.Add(ToSummary(manifest));
        }

        return summaries
            .OrderByDescending(summary => summary.CreatedAtUtc)
            .ToList();
    }

    public RecoveryManifest? GetSessionManifest(string sessionId) => LoadManifest(sessionId);

    public Task<RecoveryRestoreResult> RestoreSessionAsync(
        string sessionId,
        RestoreConflictPolicy conflictPolicy = RestoreConflictPolicy.Skip,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var manifest = LoadManifest(sessionId)
                ?? throw new InvalidOperationException($"Recovery session '{sessionId}' was not found.");

            var results = new List<RecoveryRestoreItemResult>();
            var updatedItems = manifest.Items.ToList();

            for (var index = 0; index < updatedItems.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var item = updatedItems[index];
                if (item.Status != RecoveryItemStatus.Staged)
                {
                    continue;
                }

                var result = RestoreItemCore(manifest, item, conflictPolicy);
                results.Add(result);

                if (result.Restored)
                {
                    updatedItems[index] = item with { Status = RecoveryItemStatus.Restored };
                }
            }

            SaveManifest(manifest with { Items = updatedItems });
            return new RecoveryRestoreResult { SessionId = sessionId, Items = results };
        }, cancellationToken);
    }

    public Task<RecoveryRestoreItemResult> RestoreItemAsync(
        string sessionId,
        string itemId,
        RestoreConflictPolicy conflictPolicy = RestoreConflictPolicy.Skip,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var manifest = LoadManifest(sessionId)
                ?? throw new InvalidOperationException($"Recovery session '{sessionId}' was not found.");

            var item = manifest.Items.FirstOrDefault(candidate => candidate.ItemId == itemId)
                ?? throw new InvalidOperationException($"Recovery item '{itemId}' was not found.");

            if (item.Status != RecoveryItemStatus.Staged)
            {
                return new RecoveryRestoreItemResult
                {
                    ItemId = itemId,
                    OriginalPath = item.OriginalPath,
                    Restored = false,
                    Skipped = true,
                    Renamed = false,
                    Message = $"Item status is '{item.Status}', restore skipped."
                };
            }

            var result = RestoreItemCore(manifest, item, conflictPolicy);
            if (result.Restored)
            {
                var updatedItems = manifest.Items
                    .Select(candidate => candidate.ItemId == itemId
                        ? candidate with { Status = RecoveryItemStatus.Restored }
                        : candidate)
                    .ToList();

                SaveManifest(manifest with { Items = updatedItems });
            }

            return result;
        }, cancellationToken);
    }

    public Task PurgeSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            PurgeSessionCore(sessionId);
        }, cancellationToken);
    }

    internal void PurgeSessionCore(string sessionId)
    {
        var manifest = LoadManifest(sessionId);
        if (manifest is null)
        {
            return;
        }

        foreach (var item in manifest.Items.Where(item => item.Status == RecoveryItemStatus.Staged))
        {
            if (_fileSystem.FileExists(item.StagedPath))
            {
                _fileSystem.DeleteFile(item.StagedPath);
            }
        }

        var updatedItems = manifest.Items
            .Select(item => item with { Status = RecoveryItemStatus.Purged })
            .ToList();

        SaveManifest(manifest with
        {
            Status = RecoverySessionStatus.Purged,
            Items = updatedItems
        });

        var sessionDirectory = RecoveryPathHelper.GetSessionDirectory(_options, sessionId);
        if (_fileSystem.DirectoryExists(sessionDirectory))
        {
            _fileSystem.DeleteDirectory(sessionDirectory, recursive: true);
        }

        _logger.LogInformation("Recovery session purged: {SessionId}", sessionId);
    }

    private RecoveryRestoreItemResult RestoreItemCore(
        RecoveryManifest manifest,
        RecoveryManifestItem item,
        RestoreConflictPolicy conflictPolicy)
    {
        if (!_fileSystem.FileExists(item.StagedPath))
        {
            return new RecoveryRestoreItemResult
            {
                ItemId = item.ItemId,
                OriginalPath = item.OriginalPath,
                Restored = false,
                Skipped = true,
                Renamed = false,
                Message = "Staged file no longer exists."
            };
        }

        var destinationPath = item.OriginalPath;
        var validation = _safetyValidator.Validate(destinationPath);
        if (!validation.IsAllowed)
        {
            return new RecoveryRestoreItemResult
            {
                ItemId = item.ItemId,
                OriginalPath = item.OriginalPath,
                Restored = false,
                Skipped = true,
                Renamed = false,
                Message = $"Restore destination failed safety validation: {validation.Reason}"
            };
        }

        if (_fileSystem.FileExists(destinationPath))
        {
            var existingHash = _fileSystem.ComputeSha256Hash(destinationPath);
            if (string.Equals(existingHash, item.Sha256Hash, StringComparison.OrdinalIgnoreCase))
            {
                _fileSystem.DeleteFile(item.StagedPath);
                return new RecoveryRestoreItemResult
                {
                    ItemId = item.ItemId,
                    OriginalPath = item.OriginalPath,
                    Restored = true,
                    Skipped = false,
                    Renamed = false,
                    RestoredPath = destinationPath,
                    Message = "Original file already exists with matching hash."
                };
            }

            if (conflictPolicy == RestoreConflictPolicy.Skip)
            {
                return new RecoveryRestoreItemResult
                {
                    ItemId = item.ItemId,
                    OriginalPath = item.OriginalPath,
                    Restored = false,
                    Skipped = true,
                    Renamed = false,
                    Message = "Restore skipped because a different file already exists at the original path."
                };
            }

            destinationPath = BuildRenamedRestorePath(item.OriginalPath);
        }

        var destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            _fileSystem.CreateDirectory(destinationDirectory);
        }

        _fileSystem.MoveFile(item.StagedPath, destinationPath);

        var restoredHash = _fileSystem.ComputeSha256Hash(destinationPath);
        if (!string.Equals(restoredHash, item.Sha256Hash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Restored file hash mismatch for item '{item.ItemId}'. Expected {item.Sha256Hash}, got {restoredHash}.");
        }

        var renamed = !destinationPath.Equals(item.OriginalPath, StringComparison.OrdinalIgnoreCase);
        return new RecoveryRestoreItemResult
        {
            ItemId = item.ItemId,
            OriginalPath = item.OriginalPath,
            Restored = true,
            Skipped = false,
            Renamed = renamed,
            RestoredPath = destinationPath,
            Message = renamed
                ? "File restored to a renamed path because the original path was occupied."
                : "File restored successfully."
        };
    }

    private static string BuildRenamedRestorePath(string originalPath)
    {
        var directory = Path.GetDirectoryName(originalPath) ?? string.Empty;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(originalPath);
        var extension = Path.GetExtension(originalPath);
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
        var renamedFileName = $"{fileNameWithoutExtension}.restored.{timestamp}{extension}";
        return Path.Combine(directory, renamedFileName);
    }

    private RecoveryManifest? LoadManifest(string sessionId)
    {
        var manifestPath = RecoveryPathHelper.GetManifestPath(_options, sessionId);
        return _manifestStore.Load(manifestPath);
    }

    private void SaveManifest(RecoveryManifest manifest)
    {
        var manifestPath = RecoveryPathHelper.GetManifestPath(_options, manifest.SessionId);
        _manifestStore.Save(manifestPath, manifest);
    }

    private static RecoverySessionSummary ToSummary(RecoveryManifest manifest)
    {
        var status = manifest.Status;
        if (status == RecoverySessionStatus.Active && manifest.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            status = RecoverySessionStatus.Expired;
        }

        return new RecoverySessionSummary
        {
            SessionId = manifest.SessionId,
            CreatedAtUtc = manifest.CreatedAtUtc,
            ExpiresAtUtc = manifest.ExpiresAtUtc,
            Status = status,
            FileCount = manifest.Items.Count(item => item.Status == RecoveryItemStatus.Staged),
            TotalSizeBytes = manifest.Items
                .Where(item => item.Status == RecoveryItemStatus.Staged)
                .Sum(item => item.SizeBytes)
        };
    }
}
