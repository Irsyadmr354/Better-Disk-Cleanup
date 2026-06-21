using BetterDiskCleanup.Core.StartupManager;
using BetterDiskCleanup.Infrastructure.StartupManager;
using Microsoft.Extensions.Logging.Abstractions;

namespace BetterDiskCleanup.Tests.StartupManager;

public class StartupEntrySafetyValidatorTests
{
    private StartupEntrySafetyValidator CreateValidator(Func<string, bool>? isMicrosoftSigned = null)
    {
        var logger = NullLogger<StartupEntrySafetyValidator>.Instance;
        return new StartupEntrySafetyValidator(logger, isMicrosoftSigned ?? (_ => false));
    }

    // ── Protected entry detection ─────────────────────────────────────────

    [Fact]
    public void IsProtected_MicrosoftSignedInSystem32_ReturnsTrue()
    {
        var validator = CreateValidator(_ => true); // Everything "signed by Microsoft"
        var entry = new StartupEntry
        {
            Name = "WindowsSecurity",
            FilePath = @"C:\Windows\System32\security.exe",
            Source = StartupEntrySource.Registry
        };

        Assert.True(validator.IsProtected(entry));
    }

    [Fact]
    public void IsProtected_MicrosoftSignedInSysWOW64_ReturnsTrue()
    {
        var validator = CreateValidator(_ => true);
        var entry = new StartupEntry
        {
            Name = "WowApp",
            FilePath = @"C:\Windows\SysWOW64\wowapp.exe",
            Source = StartupEntrySource.Registry
        };

        Assert.True(validator.IsProtected(entry));
    }

    [Fact]
    public void IsProtected_NotSigned_ReturnsFalse()
    {
        var validator = CreateValidator(_ => false); // Nothing signed
        var entry = new StartupEntry
        {
            Name = "SomeApp",
            FilePath = @"C:\Windows\System32\someapp.exe",
            Source = StartupEntrySource.Registry
        };

        Assert.False(validator.IsProtected(entry));
    }

    [Fact]
    public void IsProtected_MicrosoftSignedButNotInSystemDir_ReturnsFalse()
    {
        var validator = CreateValidator(_ => true);
        var entry = new StartupEntry
        {
            Name = "UserApp",
            FilePath = @"C:\Users\Someone\AppData\Local\app.exe",
            Source = StartupEntrySource.Registry
        };

        Assert.False(validator.IsProtected(entry));
    }

    [Fact]
    public void IsProtected_EmptyFilePath_ReturnsFalse()
    {
        var validator = CreateValidator(_ => true);
        var entry = new StartupEntry
        {
            Name = "NoPath",
            FilePath = string.Empty,
            Source = StartupEntrySource.Registry
        };

        Assert.False(validator.IsProtected(entry));
    }

    // ── Action validation ─────────────────────────────────────────────────

    [Fact]
    public void ValidateActionAllowed_EnableOnProtected_DoesNotThrow()
    {
        var validator = CreateValidator(_ => true);
        var entry = new StartupEntry
        {
            Name = "ProtectedEntry",
            FilePath = @"C:\Windows\System32\protected.exe",
            Source = StartupEntrySource.Registry,
            IsProtected = true
        };

        // Enable should always be allowed, even on protected entries
        validator.ValidateActionAllowed(entry, StartupChangeAction.Enable);
    }

    [Fact]
    public void ValidateActionAllowed_DisableOnProtected_Throws()
    {
        var validator = CreateValidator(_ => true);
        var entry = new StartupEntry
        {
            Name = "ProtectedEntry",
            FilePath = @"C:\Windows\System32\protected.exe",
            Source = StartupEntrySource.Registry
        };

        Assert.Throws<InvalidOperationException>(() =>
            validator.ValidateActionAllowed(entry, StartupChangeAction.Disable));
    }

    [Fact]
    public void ValidateActionAllowed_RemoveOnProtected_Throws()
    {
        var validator = CreateValidator(_ => true);
        var entry = new StartupEntry
        {
            Name = "ProtectedEntry",
            FilePath = @"C:\Windows\System32\protected.exe",
            Source = StartupEntrySource.Registry
        };

        Assert.Throws<InvalidOperationException>(() =>
            validator.ValidateActionAllowed(entry, StartupChangeAction.Remove));
    }

    [Fact]
    public void ValidateActionAllowed_DisableOnNonProtected_DoesNotThrow()
    {
        var validator = CreateValidator(_ => false);
        var entry = new StartupEntry
        {
            Name = "NormalApp",
            FilePath = @"C:\Program Files\App\app.exe",
            Source = StartupEntrySource.Registry
        };

        validator.ValidateActionAllowed(entry, StartupChangeAction.Disable);
    }

    [Fact]
    public void ValidateActionAllowed_RemoveOnNonProtected_DoesNotThrow()
    {
        var validator = CreateValidator(_ => false);
        var entry = new StartupEntry
        {
            Name = "NormalApp",
            FilePath = @"C:\Program Files\App\app.exe",
            Source = StartupEntrySource.Registry
        };

        validator.ValidateActionAllowed(entry, StartupChangeAction.Remove);
    }
}
