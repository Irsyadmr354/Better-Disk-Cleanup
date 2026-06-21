using BetterDiskCleanup.Core.StartupManager;
using Microsoft.Extensions.Logging;

namespace BetterDiskCleanup.Infrastructure.StartupManager;

/// <summary>
/// Estimates startup impact using transparent, simple heuristics.
///
/// Criteria (NOT a precise measurement like Task Manager):
///   - **High**: Executable > 5 MB, OR not digitally signed (untrusted code takes longer to verify)
///   - **Medium**: Executable 1–5 MB with a valid signature
///   - **Low**: Executable &lt; 1 MB with a valid signature, or file not found
///
/// This is communicated to the user as an estimate, not a measurement.
/// </summary>
internal sealed class StartupImpactEstimator : IStartupImpactEstimator
{
    private readonly ILogger<StartupImpactEstimator> _logger;

    // Injectable function for signature check (allows test override)
    private readonly Func<string, bool> _hasValidSignature;

    private const long HighThresholdBytes = 5 * 1024 * 1024;   // 5 MB
    private const long MediumThresholdBytes = 1 * 1024 * 1024;  // 1 MB

    public StartupImpactEstimator(ILogger<StartupImpactEstimator> logger)
        : this(logger, path => HasAuthenticodeSignature(path))
    {
    }

    internal StartupImpactEstimator(
        ILogger<StartupImpactEstimator> logger,
        Func<string, bool> hasValidSignature)
    {
        _logger = logger;
        _hasValidSignature = hasValidSignature;
    }

    public StartupImpactLevel Estimate(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return StartupImpactLevel.Low;

        try
        {
            if (!File.Exists(filePath))
                return StartupImpactLevel.Low;

            var fileInfo = new FileInfo(filePath);
            var sizeBytes = fileInfo.Length;
            var isSigned = _hasValidSignature(filePath);

            // Unsigned executables are considered high impact (slower verification, less trust)
            if (!isSigned)
                return StartupImpactLevel.High;

            // Signed executables classified by size
            if (sizeBytes >= HighThresholdBytes)
                return StartupImpactLevel.High;

            if (sizeBytes >= MediumThresholdBytes)
                return StartupImpactLevel.Medium;

            return StartupImpactLevel.Low;
        }
        catch
        {
            return StartupImpactLevel.Low;
        }
    }

    private static bool HasAuthenticodeSignature(string filePath)
    {
        try
        {
            using var cert = System.Security.Cryptography.X509Certificates
                .X509Certificate2.CreateFromSignedFile(filePath);
            return true; // If we can create a cert from the signed file, it has a signature
        }
        catch
        {
            return false;
        }
    }
}
