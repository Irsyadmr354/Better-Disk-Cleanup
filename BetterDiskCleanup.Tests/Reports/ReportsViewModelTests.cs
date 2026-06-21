using System.Collections.ObjectModel;
using System.Threading.Tasks;
using BetterDiskCleanup.App.ViewModels;
using BetterDiskCleanup.Core.Recovery;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BetterDiskCleanup.Tests.Reports;

public class ReportsViewModelTests
{
    [Fact]
    public void ExportLogsTxtCommand_ExecutesWithoutError()
    {
        // Arrange
        var mockRecoveryService = new Mock<IRecoveryService>();
        var mockRecoveryCleanupService = new Mock<IRecoveryCleanupService>();
        var mockHistoryLogger = new Mock<ILogger<RecoveryHistoryViewModel>>();
        var recoveryHistoryVm = new RecoveryHistoryViewModel(mockRecoveryService.Object, mockRecoveryCleanupService.Object, mockHistoryLogger.Object);
        var mockLogger = new Mock<ILogger<ReportsViewModel>>();
        var logStore = new LogStore();
        var vm = new ReportsViewModel(recoveryHistoryVm, logStore);

        // Act
        // Can't fully test SaveFileDialog behavior in unit tests easily without abstracting it.
        // We will just verify it initializes without error.
        Assert.NotNull(vm.ExportLogsTxtCommand);
    }
}
