# Architecture

Better Disk Cleanup is built using the Model-View-ViewModel (MVVM) architecture on top of Windows Presentation Foundation (WPF) with .NET 8. The application heavily utilizes Dependency Injection (DI) through `Microsoft.Extensions.DependencyInjection` to ensure loose coupling, testability, and a clear separation of concerns.

## Solution Structure

The solution is divided into four main projects (Clean Architecture approach):

### 1. BetterDiskCleanup.Core
The core domain model, interfaces, and business rules. It has NO dependencies on any specific technology or framework (other than .NET Base Class Library).
- **Analysis**: Models for scan results, items, and warnings.
- **Browsers**: Abstractions for browser detection and scanning (`IBrowserDetector`, `IBrowserDataScanner`).
- **Cleanup**: Interfaces for simulating and executing cleanup (`ICleanupSimulator`, `ICleanupExecutor`).
- **Duplicates / LargeFiles**: Core logic for file hashing, size matching, and handling items.
- **Recovery**: The manifest model and `IRecoveryService` for undoing file deletions.
- **Safety**: Whitelisting algorithms and the `PathSafetyValidator`.
- **StartupManager**: Models for managing startup entries (`StartupEntry`).
- **StorageAnalyzer**: In-memory representations of storage hierarchies (`FolderNode`, `TreemapRect`).

### 2. BetterDiskCleanup.Infrastructure
Contains the concrete implementations of the interfaces defined in the Core project. It deals with the OS, file system, registry, and external libraries.
- **Filesystem**: File I/O operations (`FileSystemGateway`).
- **Monitoring**: Background service for real-time disk monitoring (`StorageMonitorService`).
- **StartupManager**: Registry readers (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`), COM Task Scheduler parsing (`ComScheduledTaskReader`).
- **DependencyInjection.cs**: The central place to register all services into the `IServiceCollection`.

### 3. BetterDiskCleanup.App
The presentation layer.
- **ViewModels**: Responsible for binding data to the views. Handles state, navigation (`MainShellViewModel`), and commands using `AsyncRelayCommand`.
- **Views**: XAML definitions (`MainWindow.xaml`).
- **Controls**: Custom UI elements like `TreemapControl` and `PieChartControl`.
- **App.xaml.cs**: Sets up the Generic Host (`IHost`) to provide dependency injection, configuration, and logging to the WPF application.

### 4. BetterDiskCleanup.Tests
Unit tests using xUnit, Moq, and FluentAssertions. Provides thorough coverage of Core models and Infrastructure components using an `InMemoryFileSystemGateway` to mock disk interactions safely.

## Key Design Patterns

- **Dependency Injection**: Used universally across ViewModels and Services.
- **Command Pattern**: Implemented via `AsyncRelayCommand` for asynchronous WPF interactions without locking the UI thread.
- **BackgroundService (Worker)**: Used via `Microsoft.Extensions.Hosting` for long-running background tasks (e.g., Disk Space Monitoring) while keeping the WPF UI responsive.
- **Strategy Pattern**: Used for picking duplicate deletion strategies (`KeepNewestStrategy`, `KeepOldestStrategy`).
- **Observer Pattern / Events**: Used for updating the UI during long-running tasks using `IProgress<T>` and `INotifyPropertyChanged`.

## Recovery System (Undo)
Before executing a cleanup, the system calculates a simulation. When executing, the file isn't deleted immediately. Instead, it is zipped and moved to an isolated `%AppData%` folder with a tracking `manifest.json`. The `RecoveryService` is able to parse this manifest and restore files back to their exact original paths.

## Startup Entry Safety
Windows registry values for Startup are often critical. We implement a non-destructive `StartupChangeRecoveryService` which acts as a specialized undo log specifically for Registry/Startup Folder/Scheduled Task operations.
