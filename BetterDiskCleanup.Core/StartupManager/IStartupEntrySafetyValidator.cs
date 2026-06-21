namespace BetterDiskCleanup.Core.StartupManager;

/// <summary>
/// Validates whether a startup entry is a protected system component.
/// Protected entries cannot be disabled or removed — the validation happens
/// at the service/business-logic level, not just the UI.
///
/// An entry is considered Protected when:
///   1. Its executable is digitally signed by Microsoft, AND
///   2. Its executable resides in a Windows system directory (System32, SysWOW64, Windows\)
/// </summary>
public interface IStartupEntrySafetyValidator
{
    /// <summary>
    /// Returns true if the entry is a protected system component.
    /// </summary>
    bool IsProtected(StartupEntry entry);

    /// <summary>
    /// Validates that the given action is allowed on the entry.
    /// Throws <see cref="InvalidOperationException"/> if the entry is Protected
    /// and the action is Disable or Remove.
    /// </summary>
    void ValidateActionAllowed(StartupEntry entry, StartupChangeAction action);
}
