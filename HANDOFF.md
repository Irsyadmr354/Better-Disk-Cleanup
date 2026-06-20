# Handoff Summary — Better Disk Cleanup (Fase 0–3B Complete)

## Project Info
- **Solution path:** `C:\Users\Royan\Documents\Better Disk Cleanup`
- **Tech stack:** C# .NET 8, WPF, Clean Architecture + MVVM, Microsoft.Extensions.Hosting, Serilog, xUnit
- **Status:** Fase 0–3B selesai, 90/90 tests PASS

---

## Fase yang Sudah Selesai

### Fase 0 — Safety System
- `IPathSafetyValidator` di `BetterDiskCleanup.Core/Safety/`
- Whitelist-first validation, resolve symlink/junction, blacklist System32/Program Files/user folders
- **TIDAK BOLEH diubah logic-nya** — fase selanjutnya bergantung penuh ke sini
- Di Fase 3A: ditambah 6 whitelist entry baru untuk browser data dirs (Chrome, Edge, Brave, Opera, Vivaldi, Firefox) via `WhitelistPathTemplate` enum + `CleanupPathWhitelist` + `WhitelistPathResolver`

### Fase 1 — Cleanup Engine
- `ITempFileScanner`, `ICleanupSimulator`, `ICleanupExecutor` di `BetterDiskCleanup.Core/`
- Scan `%TEMP%`, simulate, delete dengan re-validasi safety per-file
- Clear `FileAttributes.ReadOnly` sebelum delete, pisah `Errors` vs `SkippedInUse` di report
- Signature publik `ICleanupExecutor`/`ICleanupSimulator` **jangan diubah** kalau tidak terpaksa

### Fase 2 — Recovery System
- `IRecoverySnapshotService`, `IRecoveryService`, `IRecoveryCleanupService` di `BetterDiskCleanup.Core/Recovery/`
- Sebelum delete permanen, file dipindah ke staging `%TEMP%\BetterDiskCleanup\Recovery\{session-id}\`
- Manifest JSON dengan SHA-256 hash, restore per-session/per-item, retention 30 hari configurable
- Manual verification (restore, konflik path, purge) belum sempat dilakukan user

### Fase 3A — Browser Cleanup Module (baru selesai)

**Core interfaces** (`BetterDiskCleanup.Core/Browsers/`):
- `IBrowserDetector` — deteksi browser + semua profile (Chrome, Brave, Edge, Firefox, Opera, Vivaldi)
- `IBrowserDataScanner` — scan & kategorisasi: Cache, Cookies, History, Sessions, ServiceWorker, Temporary
- `IBrowserProcessChecker` — cek apakah browser sedang running via `Process.GetProcessesByName()`

**Infrastructure** (`BetterDiskCleanup.Infrastructure/Browsers/`):
- `IBrowserAdapter` (internal) — abstraction per engine
- `ChromiumBrowserAdapter` — parse `Local State` JSON (semua Chromium-based browsers)
- `FirefoxBrowserAdapter` — parse `profiles.ini`
- `BrowserDetector` — aggregates semua adapter
- `BrowserProcessChecker` — real process check + testable via `Func<string, bool>` override
- `BrowserDataScanner` — enumerate files, map risk levels (Cache=Safe, Cookies/History=Advanced)

**UI** (`BetterDiskCleanup.App/`):
- `BrowserCleanupViewModel` — tab baru "Browser Cleanup" di MainWindow
- Detect & Scan → Preview → Clean flow (reuse existing `ICleanupSimulator` + `ICleanupExecutor`)
- Cookies/History **tidak auto-select** (RiskLevel.Advanced), user harus centang manual
- Warning merah kalau browser terdeteksi sedang berjalan

**Risk level rationale:**
- Cache/Temporary = `Safe` (auto-select, browser regenerate)
- ServiceWorker/Sessions = `Recommended` (auto-select)
- Cookies/History = `Advanced` (manual only — berisi login sessions & browsing history, irreplaceable)

### Fase 3B — Large File Finder (baru selesai)

**Core interfaces** (`BetterDiskCleanup.Core/LargeFiles/`):
- `ILargeFileScanner` — scan drive/folder untuk file besar dengan threshold configurable
- `LargeFileEntry` — model per file: nama, ekstensi, kategori (Video/Archive/DiskImage/Document/Other), ukuran, last modified
- `LargeFileScanProgress` — progress DTO untuk IProgress<T> (files found, bytes scanned)
- `FileCategory` — enum: Video, Archive, DiskImage, Document, Other

**Infrastructure** (`BetterDiskCleanup.Infrastructure/LargeFiles/`):
- `LargeFileScanner` — parallel scan pakai `Parallel.ForEachAsync` dengan `MaxDegreeOfParallelism = min(Processors, 8)`
- Enumerate directories paralel, files per-directory sequential (batas I/O concurrency natural)
- `ConcurrentBag<T>` untuk thread-safe result collection
- Graceful handling access-denied (log warning, skip folder, lanjut)
- Skip reparse points (symlink/junction) untuk hindari infinite loop
- Extension-to-category mapping (26 ekstensi supported)
- `GetAvailableDrives()` — wrap `DriveInfo.GetDrives()` (fixed drives only)

**IFileSystemGateway modification:**
- Tambah `EnumerateFilesDirect(string directoryPath)` — non-recursive file enumeration per directory
- Diperlukan supaya scanner bisa proses directory secara paralel (bukan recursive dari root)
- Implementasi di `FileSystemGateway`, `InMemoryFileSystemGateway`, `TrackingFileSystemGateway`

**UI** (`BetterDiskCleanup.App/`):
- `LargeFileFinderViewModel` — tab baru "Large Files" (index 3) di MainWindow
- Drive selector + threshold dropdown (100MB/500MB/1GB/5GB) + Scan/Cancel
- Search box (substring filter by nama/lokasi)
- ListView dengan kolom: checkbox, FileName, Size, Type, Location, Modified
- Context menu: Open File (`Process.Start` UseShellExecute), Open Folder (`explorer /select`)
- Move file via `IFileSystemGateway.MoveFile` (bukan lewat recovery — ini operasi pindah biasa)
- Delete file WAJIB lewat pipeline `ICleanupExecutor` (safety re-validasi → recovery staging → delete)
- Multi-select delete dengan konfirmasi jumlah file + total size

**Parallelism strategy:**
- `Parallel.ForEachAsync` atas list directories (bukan files)
- Cap di `min(Environment.ProcessorCount, 8)` untuk hindari overwhelming disk I/O
- `Interlocked` operations untuk counter (filesFound, bytesScanned)
- `ConcurrentBag<T>` untuk hasil scan (lock-free)

---

## Fix Urgent yang Sudah Dilakukan (Sebelum Fase 3A)

### Bug: App tidak bisa start (silent crash)

**Penyebab sebenarnya:** `appsettings.json` di-load pakai relative path, tapi `dotnet run` dari solution root set CWD ke solution dir bukan project output dir. File tidak ketemu → `FileNotFoundException`.

**Penyebab amplifier:** `OnStartup` adalah `async void` tanpa exception handling → exception silent-kill app, tidak ada log.

**Fix di `App.xaml.cs`:**
1. `AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), ...)` — resolve relative to assembly
2. `EnsureRecoveryStagingDirectoryExists()` — pre-create `%TEMP%\BetterDiskCleanup\Recovery` sebelum DI
3. `_host.StartAsync().GetAwaiter().GetResult()` — hapus `async void`, sync wait di try-catch
4. Global exception handlers: `AppDomain.CurrentDomain.UnhandledException`, `DispatcherUnhandledException`, `TaskScheduler.UnobservedTaskException`
5. Fallback crash log ke `%LocalAppData%\BetterDiskCleanup\logs\crash-fallback.log` (plain text, tidak depend on Serilog)

---

## Struktur Project (File yang Baru/Diubah di Fase 3B)

```
BetterDiskCleanup.Core/
├── LargeFiles/                        [BARU]
│   ├── FileCategory.cs
│   ├── LargeFileEntry.cs
│   ├── LargeFileScanProgress.cs
│   ├── LargeFileScanResult.cs
│   └── ILargeFileScanner.cs
└── Filesystem/
    └── IFileSystemGateway.cs          [MODIFIED - tambah EnumerateFilesDirect]

BetterDiskCleanup.Infrastructure/
├── LargeFiles/                        [BARU]
│   └── LargeFileScanner.cs
├── Filesystem/
│   └── FileSystemGateway.cs           [MODIFIED - implement EnumerateFilesDirect]
└── DependencyInjection.cs             [MODIFIED - register ILargeFileScanner]

BetterDiskCleanup.App/
├── ViewModels/
│   ├── LargeFileFinderViewModel.cs    [BARU]
│   ├── MainShellViewModel.cs          [MODIFIED - tambah LargeFileFinderViewModel, IsLargeFilesPage]
│   ├── RelayCommand.cs                [MODIFIED - tambah RelayCommand<T>]
│   └── AsyncRelayCommand.cs           [MODIFIED - tambah AsyncRelayCommand<T>]
├── MainWindow.xaml                    [MODIFIED - tambah "Large Files" tab (index 3)]
└── App.xaml.cs                        [MODIFIED - register LargeFileFinderViewModel]

BetterDiskCleanup.Tests/
├── LargeFiles/                        [BARU]
│   ├── LargeFileScannerThresholdTests.cs  (5 tests)
│   ├── LargeFileScannerSortingTests.cs    (6 tests)
│   ├── LargeFileScannerAccessDeniedTests.cs (2 tests)
│   ├── LargeFileDeleteSafetyTests.cs      (3 tests)
│   └── LargeFileCategoryMappingTests.cs   (31 theory cases)
├── Integration/
│   └── LargeFileCleanupIntegrationTests.cs [BARU] (2 tests)
└── Support/
    ├── InMemoryFileSystemGateway.cs   [MODIFIED - tambah EnumerateFilesDirect]
    └── TrackingFileSystemGateway.cs   [MODIFIED - tambah EnumerateFilesDirect]
```

---

## Test Summary
- **Total: 90 tests, 90 passed, 0 failed**
- 43 existing (Fase 0–3A): Safety, Cleanup, Recovery, Scanning, Browser, Integration
- 47 new (Fase 3B): Threshold filtering, sorting, access-denied resilience, safety validation, category mapping, scan-delete-recovery integration

---

## Yang TIDAK BOLEH Dilakukan
1. Jangan ubah logic `IPathSafetyValidator` / `PathSafetyValidator`
2. Jangan ubah signature publik `ICleanupExecutor` / `ICleanupSimulator` kecuali terpaksa
3. Jangan hapus/modifikasi test yang sudah ada demi coverage

---

## Rencana Selanjutnya (Fase 3C–3D, urutan bebas)
- **3C:** Duplicate Finder
- **3D:** Startup Manager

User juga masih perlu **manual verification** Recovery System (restore, konflik path, purge) yang belum sempat dilakukan.

---

## Development Rules
- `dotnet test` harus selalu hijau semua sebelum lanjut fase
- Semua cleanup WAJIB lewat pipeline `ICleanupExecutor` (yang otomatis integrate Recovery System)
- Jangan buat alur delete terpisah di luar pipeline yang ada
- Pakai `InternalsVisibleTo` di Infrastructure csproj supaya test bisa akses internal types
