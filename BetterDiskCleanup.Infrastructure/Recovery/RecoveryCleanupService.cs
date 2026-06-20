using BetterDiskCleanup.Core.Recovery;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BetterDiskCleanup.Infrastructure.Recovery;

public sealed class RecoveryCleanupService : IRecoveryCleanupService
{
    private readonly RecoveryService _recoveryService;
    private readonly IRecoveryOptions _options;
    private readonly RecoveryManifestStore _manifestStore;
    private readonly ILogger<RecoveryCleanupService> _logger;

    public RecoveryCleanupService(
        RecoveryService recoveryService,
        IOptions<RecoveryOptions> options,
        ILogger<RecoveryCleanupService> logger)
    {
        _recoveryService = recoveryService;
        _options = new RecoveryOptionsAdapter(options);
        _manifestStore = new RecoveryManifestStore();
        _logger = logger;
    }

    public Task<RecoveryPurgeResult> PurgeExpiredSessionsAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var recoveryRoot = RecoveryPathHelper.GetRecoveryRoot(_options);
            if (!Directory.Exists(recoveryRoot))
            {
                return new RecoveryPurgeResult { PurgedSessionIds = [] };
            }

            var purgedSessionIds = new List<string>();
            var now = DateTimeOffset.UtcNow;

            foreach (var sessionDirectory in Directory.EnumerateDirectories(recoveryRoot))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var sessionId = Path.GetFileName(sessionDirectory);
                var manifest = _manifestStore.Load(RecoveryPathHelper.GetManifestPath(_options, sessionId));
                if (manifest is null)
                {
                    continue;
                }

                if (manifest.Status == RecoverySessionStatus.Purged)
                {
                    continue;
                }

                if (manifest.ExpiresAtUtc <= now)
                {
                    _recoveryService.PurgeSessionCore(sessionId);
                    purgedSessionIds.Add(sessionId);
                }
            }

            _logger.LogInformation("Purged {Count} expired recovery sessions.", purgedSessionIds.Count);
            return new RecoveryPurgeResult { PurgedSessionIds = purgedSessionIds };
        }, cancellationToken);
    }
}
