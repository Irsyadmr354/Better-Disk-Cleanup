namespace BetterDiskCleanup.Core.Cleanup;

public enum CleanupFailureStage
{
    SafetyRevalidation,
    FileNotFound,
    SizeRead,
    AttributeChange,
    FileInUse,
    DeleteFile
}
