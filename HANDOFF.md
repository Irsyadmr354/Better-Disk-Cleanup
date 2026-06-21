# Handoff Summary — Better Disk Cleanup (Fase 0–3D Complete)

## Project Info
- **Solution path:** `C:\Users\Royan\Documents\Better Disk Cleanup`
- **Tech stack:** C# .NET 8, WPF, Clean Architecture + MVVM, Microsoft.Extensions.Hosting, Serilog, xUnit
- **Status:** Fase 0–3D selesai, 135/135 tests PASS
- **Runtime:** Requires Administrator privileges (auto-elevates via UAC on startup)

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

### Fase 3A — Browser Cleanup Module

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

### Fase 3B — Large File Finder

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

**BFS traversal fix (post-Fase 3B):**
- Scanner pakai BFS level-by-level traversal (bukan recursive `EnumerateDirectories`)
- `EnumerateDirectoriesDirect(string)` — non-recursive, returns only immediate children
- Added to `IFileSystemGateway`, `FileSystemGateway`, `InMemoryFileSystemGateway`, `TrackingFileSystemGateway`
- `.ToList()` materialization inside try-catch untuk capture lazy enumeration exceptions
- `ShouldSkipReparsePoint` wrapped in per-directory try-catch
- Counters pakai `int[]`/`long[]` arrays (bukan `ref` parameters) supaya compatible dengan lambda

**Auto-Admin elevation (`App.xaml.cs`):**
- On startup, cek `WindowsPrincipal.IsInRole(WindowsBuiltInRole.Administrator)`
- Kalau bukan admin → restart dengan `Verb = "runas"` (trigger UAC)
- Handle `dotnet run` scenario: detect kalau host adalah `dotnet.exe`, restart pakai `dotnet.exe "path/to/dll"`
- Graceful fallback kalau UAC cancelled → MessageBox dengan instruksi manual

### Fase 3C — Duplicate File Finder (baru selesai)

**Core interfaces & models** (`BetterDiskCleanup.Core/Duplicates/`):
- `IDuplicateFileScanner` — scan folder/drive untuk duplikat dengan strategi 2 tahap (size grouping → SHA256 hashing)
- `IDuplicateSelectionStrategy` — interface untuk strategi seleksi file mana yang dihapus (KeepNewest/KeepOldest/KeepOriginal/Manual)
- `DuplicateFileEntry` — model per file: path, fileName, sizeBytes, lastModifiedUtc, createdUtc
- `DuplicateGroup` — kelompok duplikat: hash, fileSizeBytes, members list, locationType (SameFolderDifferentName / DifferentFolder), recoverableBytes
- `DuplicateScanProgress` — progress DTO: totalFilesFound, sameSizeCandidates, filesHashed, duplicateGroupsFound
- `DuplicateScanResult` — hasil scan: groups, warnings, totalRecoverableBytes, totalDuplicateFiles
- `SelectionStrategyType` — enum: KeepNewest, KeepOldest, KeepOriginal, Manual
- `DuplicateLocationType` — enum: SameFolderDifferentName, DifferentFolder

**Detection strategy (2-phase):**
- **Phase 1 — Group by size:** file dengan ukuran berbeda pasti bukan duplikat → eliminasi cepat tanpa hashing (~90% files eliminated)
- **Phase 2 — Hash candidates:** SHA256 paralel via `Parallel.ForEachAsync` cap `min(Processors, 8)` hanya untuk file dengan ukuran sama
- BFS directory traversal (reuse pattern dari LargeFileScanner)
- Skip empty files (size == 0)
- Skip reparse points
- `IProgress<DuplicateScanProgress>` untuk report progress (files found, candidates, hashing progress)
- Optional partial-hash helper (`ComputePartialHash`) untuk future optimization (hash 64KB pertama sebagai quick filter)

**Selection strategies** (`BetterDiskCleanup.Infrastructure/Duplicates/`):
- `KeepNewestStrategy` — keep file dengan `LastModifiedUtc` paling baru, delete sisanya
- `KeepOldestStrategy` — keep file dengan `CreatedUtc` paling lama, delete sisanya
- `KeepOriginalStrategy` — heuristik: prefer file yang TIDAK di folder transient (Downloads/Temp/Cache/AppData) → shortest path → oldest CreatedUtc sebagai tiebreaker. `TransientFolderNames` configurable di class.
- `ManualStrategy` — return empty, user pilih sendiri per file
- Semua strategi WAJIB leave minimal 1 file alive per group

**Safety: At Least 1 Survivor Per Group (3-layer enforcement):**
1. `IDuplicateSelectionStrategy.SelectForDeletion()` — strategi tidak pernah return semua members
2. `DuplicateDeletionValidator.EnsureMinimumOneSurvivor()` — validasi + koreksi sebelum kirim ke executor. Kalau semua selected → un-select newest sebagai survivor
3. ViewModel (`DuplicateFinderViewModel`) — show MessageBox kalau koreksi terjadi, sebelum konfirmasi delete

**IFileSystemGateway modification:**
- Tambah `GetCreationTimeUtc(string path)` — diperlukan untuk KeepOldest/KeepOriginal strategy
- Implementasi di `FileSystemGateway`, `InMemoryFileSystemGateway`, `TrackingFileSystemGateway`, `AccessDeniedFileSystemGateway` (test)

**UI** (`BetterDiskCleanup.App/`):
- `DuplicateFinderViewModel` — tab baru "Duplicates" (index 4) di MainWindow
- Folder path input + Scan/Cancel
- Expandable group display: klik header untuk expand/collapse, lihat individual files
- Strategy dropdown (KeepNewest/Oldest/Original/Manual) yang langsung re-apply selection ke semua group
- Per-file checkbox selection dengan PropertyChanged subscription → DeleteCommand state
- Delete via `DuplicateDeletionValidator` → `ICleanupExecutor` → masuk Recovery System (Fase 2)
- Confirmation dialog sebelum delete, menampilkan jumlah file + total size
- Post-delete: remove deleted items from groups, remove groups dengan ≤1 member
- `DuplicateGroupViewModel` — header menampilkan member count, file size, recoverable bytes, hash prefix, location type
- `DuplicateMemberViewModel` — checkbox, filename, size, modified date, location

**Delete flow:**
1. Collect selected paths dari semua groups
2. `DuplicateDeletionValidator.EnsureMinimumOneSurvivor()` — koreksi kalau semua anggota group selected
3. Build `ScanResult` dengan `RiskLevel.Safe` per item
4. `ICleanupExecutor.ExecuteAsync()` → safety re-validation → recovery staging → delete
5. Update UI: remove deleted items, collapse empty groups

### Fase 3D — Startup Manager (baru selesai)

**Core interfaces & models** (`BetterDiskCleanup.Core/StartupManager/`):
- `IStartupScanner` — scan registry, startup folder, dan scheduled tasks untuk menemukan startup entry
- `IStartupImpactEstimator` — estimasi impact (Low/Medium/High) berdasarkan heuristik transparan
- `IStartupEntrySafetyValidator` — validasi apakah entry protected (Microsoft-signed + system dir)
- `IStartupChangeRecoveryService` — snapshot/undo/restore untuk perubahan startup entry
- `IStartupEntryManager` — orchestrator Enable/Disable/Remove dengan safety + recovery
- `StartupEntry` — model per entry: Name, Publisher, FilePath, Status, Source, Impact, IsProtected
- `StartupEntrySource` — enum: StartupFolder, Registry, ScheduledTask
- `StartupEntryStatus` — enum: Enabled, Disabled
- `StartupImpactLevel` — enum: Low, Medium, High
- `StartupChangeAction` — enum: Enable, Disable, Remove
- `StartupChangeRecord` — record perubahan: ChangeId, Action, SnapshotBefore, IsUndone
- `StartupEntrySnapshot` — immutable snapshot state sebelum perubahan

**Infrastructure** (`BetterDiskCleanup.Infrastructure/StartupManager/`):
- `StartupScanner` — scan HKCU/HKLM Run/RunOnce, user/all-users Startup folder, scheduled tasks via COM
- `ComScheduledTaskReader` — COM interop `Schedule.Service` untuk enumerate task dengan logon/boot trigger
- `FileSignatureHelper` — extract publisher, cek signature Microsoft, resolve shortcut target
- `StartupEntrySafetyValidator` — Protected = Microsoft-signed AND di System32/SysWOW64/Windows\
- `StartupImpactEstimator` — High: >5MB atau unsigned, Medium: 1–5MB signed, Low: <1MB signed
- `StartupChangeRecoveryService` — backup registry values, task XML, shortcut bytes; persist history JSON
- `StartupEntryManager` — Disable: pindah value ke backup key; Enable: restore dari backup; Remove: delete + snapshot

**Safety: Protected Entry (2-layer enforcement):**
- `IStartupEntrySafetyValidator.IsProtected()` — Microsoft-signed AND di system directory
- `ValidateActionAllowed()` — throw `InvalidOperationException` kalau Disable/Remove pada Protected entry
- UI: Toggle/Remove buttons di-disable via `InvertBoolConverter`, tapi validasi juga di service level

**Disable approach ("pindah ke backup key", bukan langsung hapus):**
- Registry: value dipindah ke `HKCU\Software\BetterDiskCleanup\StartupBackup\{valueName}`, original dihapus
- Scheduled Task: pakai native `task.Enabled = false` API (reversible tanpa backup)
- Startup Folder: shortcut `.lnk` dipindah ke `%TEMP%\BetterDiskCleanup\StartupBackup\`
- Ini supaya Disable reversible tanpa kehilangan data asli (command line string, task definition)

**Recovery System (terpisah dari Fase 2):**
- `IStartupChangeRecoveryService` — khusus untuk registry/task/shortcut, bukan file biasa
- `CreateSnapshot()` — simpan state sebelum perubahan
- `UndoLastChangeAsync()` — undo perubahan terakhir yang belum di-undo
- `RestoreFromHistoryAsync(changeId)` — restore perubahan spesifik
- History disimpan sebagai JSON di `%LocalAppData%\BetterDiskCleanup\startup-change-history.json`

**UI** (`BetterDiskCleanup.App/`):
- `StartupManagerViewModel` — tab baru "Startup Manager" (index 5) di MainWindow
- Scan startup entries → tampil tabel: Name, Publisher, Path, Status, Impact, Source, Actions
- Toggle switch (Enable/Disable) per row — Protected entry: buttons disabled
- Remove dengan konfirmasi — snapshot tetap disimpan
- Undo Last Change button
- `InvertBoolConverter` — converter baru untuk `IsProtected → IsEnabled` binding

---

## Post-Fase 3B Fixes

### UI Overhaul (App.xaml)
- **Color palette:** High-contrast slate/indigo theme (bg: #0F1117→#222639, text: #F1F3F8/#A0A8BE/#5E6580, accent: #6C8AFF)
- **Custom ComboBox:** Full ControlTemplate dengan `ToggleButton` untuk dropdown interactivity, custom `ComboBoxItem` style
- **Custom ListView/GridView:** `GridViewColumnHeader` dengan dark surface bg, `ListViewItem` dengan hover/selected states
- **Custom TextBox:** Dark surface bg, accent focus border, proper caret color
- **Custom ContextMenu/MenuItem:** Dark themed, rounded corners
- **Compact:** Reduced all paddings (16,8 → from 18,9), font sizes (12/11px), margins, border radii (6px)

### Large File Scanner BFS Fix
- **Root cause:** Original `EnumerateDirectories(rootPath)` was recursive — tried to collect ALL dirs on C: before processing files. Access-denied on most system dirs → 0 results.
- **Fix:** BFS level-by-level traversal pakai `EnumerateDirectoriesDirect` (non-recursive)
- **Lazy enumeration bug:** `Directory.EnumerateDirectories/Files` return lazy enumerables — exceptions thrown OUTSIDE try-catch when iterating. Fix: `.ToList()` inside try-catch.

### Drive Path Bug (the real "0 results" root cause)
- **Root cause:** `GetAvailableDrives()` trimmed trailing backslash → returned `"C:"` instead of `"C:\"`. On Windows, `Directory.EnumerateDirectories("C:")` enumerates the **current directory on that drive** (usually `System32` for admin), NOT the drive root → 0 subdirs found → scan completed instantly with nothing.
- **Fix:** Removed `TrimEnd` — drive paths now keep trailing backslash (`"C:\"`) so `Directory.EnumerateDirectories` correctly enumerates the root.

### Auto-Admin Elevation
- App requires Administrator privileges for full drive scanning
- Checks on startup, auto-restarts with UAC elevation if not admin
- Handles `dotnet run` scenario (detects `dotnet.exe` host, passes DLL path as argument)
- Graceful fallback with MessageBox if UAC cancelled

### Live Log Panel
- **`LogStore`** (`ViewModels/LogStore.cs`) — Serilog `ILogEventSink` that feeds log events into `ObservableCollection<string>` (capped at 200 entries, thread-safe via Dispatcher)
- **UI:** Bottom panel (160px, `Consolas` 11px) with auto-scroll, Clear/Hide buttons
- **Sidebar:** "Toggle Log" button above version label
- **`MainShellViewModel`:** Exposes `LogStore`, `IsLogVisible`, `ToggleLogCommand`, `ClearLogCommand`
- **Serilog wiring:** `LogStore` registered as singleton, added as sink in `App.xaml.cs`
- **Scanner logging:** BFS level progress, directories processed, access-denied counts, files found

### Scan UX Improvements
- **Default threshold changed:** 1GB → 100MB (user's largest file was ~672MB, 1GB threshold returned 0 results silently)
- **Directory-level progress:** `LargeFileScanProgress.DirectoriesScanned` field added; progress now reports after each BFS level AND per-directory, so UI shows activity immediately
- **Progress text format:** `Scanned 1,234 dirs | Found 5 file(s) (2.3 GB)` — shows scan is alive even before files are found
- **0-files hint:** When scan returns 0 results, status shows "No large files found — try lowering the threshold" instead of generic "Scan completed"
- **Startup log:** Scanner logs threshold in MB at scan start for easy diagnosis

### LogStore Crash Fix (VirtualizingStackPanel desync)
- **Root cause:** `LogStore.Emit` was called from multiple background threads, each scheduling its own `Dispatcher.BeginInvoke`. Rapid individual `Add()` calls desynchronized the WPF `VirtualizingStackPanel` item count.
- **Crash:** `InvalidOperationException: Accumulated count N is different from actual count M` during `ScrollIntoView` layout pass.
- **Fix:** `LogStore` now buffers entries in a `List<string>` (protected by lock) and flushes all buffered entries in a **single** `Dispatcher.BeginInvoke` call. Only one dispatcher operation is ever pending.
- **Safety net:** `MainWindow.xaml.cs` auto-scroll now wraps `ScrollIntoView` in try-catch to ignore transient layout exceptions.

### Full Project Audit — 3 UI State Bugs Fixed

**Bug 1: BrowserCleanupViewModel — Preview/Clean buttons stale on checkbox toggle**
- **Root cause:** `PreviewCommand` and `CleanCommand` use `HasSelectedItems` in `canExecute`, but toggling a `BrowserDataEntryViewModel.IsSelected` checkbox never raised `CanExecuteChanged`.
- **Fix:** Subscribe to each `BrowserDataEntryViewModel.PropertyChanged`; when `IsSelected` changes, call `RaiseCanExecuteChanged()` on both commands.

**Bug 2: LargeFileFinderViewModel — Delete button stale on item selection**
- **Root cause:** Same pattern — `DeleteCommand` depends on `HasSelectedItems`, but checking/unchecking a `LargeFileItemViewModel` never notified the command.
- **Fix:** Subscribe to each `LargeFileItemViewModel.PropertyChanged`; when `IsSelected` changes, call `RaiseCanExecuteChanged()` on `DeleteCommand`.

**Bug 3: RecoverySessionViewModel — Expired sessions displayed as "Active"**
- **Root cause:** Manifest stores `Status = Active` but expiration (`ExpiresAtUtc`) is a separate field. The ViewModel displayed the raw manifest status, so expired sessions still showed "Active" with clickable Restore/Purge buttons.
- **Fix:** Compute effective status in constructor and `RefreshAsync`: `if (Status == Active && ExpiresAtUtc <= Now) → Expired`. The `RestoreSessionCommand`/`PurgeSessionCommand` canExecute already checks `Status == Active`, so expired sessions correctly disable those buttons.

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

## Struktur Project (File yang Baru/Diubah Post-Fase 3C)

```
BetterDiskCleanup.Core/
├── Duplicates/                                    [NEW - Fase 3C]
│   ├── IDuplicateFileScanner.cs                   [NEW]
│   ├── IDuplicateSelectionStrategy.cs             [NEW]
│   ├── DuplicateFileEntry.cs                      [NEW]
│   ├── DuplicateGroup.cs                          [NEW]
│   ├── DuplicateLocationType.cs                   [NEW]
│   ├── DuplicateScanProgress.cs                   [NEW]
│   ├── DuplicateScanResult.cs                     [NEW]
│   └── SelectionStrategyType.cs                   [NEW]
├── Filesystem/
│   └── IFileSystemGateway.cs          [MODIFIED - tambah GetCreationTimeUtc]
└── LargeFiles/
    └── LargeFileScanProgress.cs       [MODIFIED - tambah DirectoriesScanned field]

BetterDiskCleanup.Infrastructure/
├── Duplicates/                                    [NEW - Fase 3C]
│   ├── DuplicateFileScanner.cs                    [NEW - 2-phase BFS scan + parallel SHA256]
│   ├── DuplicateSelectionStrategies.cs            [NEW - 4 strategies: Newest/Oldest/Original/Manual]
│   └── DuplicateDeletionValidator.cs              [NEW - ensure min 1 survivor per group]
├── LargeFiles/
│   └── LargeFileScanner.cs            [MODIFIED - BFS traversal, dir-level progress reporting]
├── Filesystem/
│   └── FileSystemGateway.cs           [MODIFIED - implement GetCreationTimeUtc]
└── DependencyInjection.cs             [MODIFIED - register IDuplicateFileScanner + strategies]

BetterDiskCleanup.App/
├── App.xaml                           [MODIFIED - version label → Fase 3C]
├── App.xaml.cs                        [MODIFIED - register DuplicateFinderViewModel]
├── MainWindow.xaml                    [MODIFIED - Duplicates nav button (index 4) + page content]
├── MainWindow.xaml.cs                 [MODIFIED - ToggleGroupExpand handler]
├── ViewModels/
│   ├── DuplicateFinderViewModel.cs    [NEW - scan, strategy, delete, group/member sub-VMs]
│   ├── MainShellViewModel.cs          [MODIFIED - DuplicateFinderViewModel + IsDuplicatesPage]
│   ├── BrowserCleanupViewModel.cs     [MODIFIED - PropertyChanged subscription]
│   ├── LargeFileFinderViewModel.cs    [MODIFIED - default 100MB, dir progress, selection state]
│   ├── LogStore.cs                    [MODIFIED - buffered flush]
│   └── RecoveryHistoryViewModel.cs    [MODIFIED - expired session status]

BetterDiskCleanup.Tests/
├── Duplicates/                                    [NEW - Fase 3C]
│   ├── DuplicateScannerTests.cs                   [NEW - 9 tests: size vs hash, location type, cancellation]
│   ├── DuplicateSelectionStrategyTests.cs         [NEW - 6 tests: all 4 strategies]
│   ├── DuplicateDeletionValidatorTests.cs         [NEW - 5 tests: min-1-survive, multi-group]
│   ├── DuplicateScannerPerformanceTests.cs        [NEW - 3 tests: parallel timing, partial hash]
│   └── DuplicateCleanupIntegrationTests.cs        [NEW - 3 tests: scan→delete→recovery→restore]
├── LargeFiles/
│   └── LargeFileScannerAccessDeniedTests.cs [MODIFIED - tambah GetCreationTimeUtc]
└── Support/
    ├── InMemoryFileSystemGateway.cs   [MODIFIED - tambah GetCreationTimeUtc + CreatedUtc field]
    └── TrackingFileSystemGateway.cs   [MODIFIED - tambah GetCreationTimeUtc]
```

---

## Test Summary
- **Total: 135 tests, 135 passed, 0 failed**
- 43 (Fase 0–3A): Safety, Cleanup, Recovery, Scanning, Browser, Integration
- 47 (Fase 3B): Threshold filtering, sorting, access-denied resilience, safety validation, category mapping, scan-delete-recovery integration
- 27 (Fase 3C): Duplicate detection (size vs hash), location type, selection strategies (Newest/Oldest/Original/Manual), min-1-survive validator, parallel hashing performance, partial hash, scan→delete→recovery→restore integration
- 18 (Fase 3D): Protected entry detection (Microsoft-signed + system dir), action validation (Enable allowed, Disable/Remove rejected), Disable→Undo registry value, Remove→Restore registry value, undo idempotency, full lifecycle (create dummy key → disable → undo → remove → restore), protected entry rejection at service level

---

## Yang TIDAK BOLEH Dilakukan
1. Jangan ubah logic `IPathSafetyValidator` / `PathSafetyValidator`
2. Jangan ubah signature publik `ICleanupExecutor` / `ICleanupSimulator` kecuali terpaksa
3. Jangan hapus/modifikasi test yang sudah ada demi coverage
4. **TIDAK ADA logic yang mengizinkan satu duplicate group kehilangan SEMUA file-nya** — ini bug kritis kalau lolos (enforced di 3 layer: strategy, validator, ViewModel)
5. **TIDAK ADA modifikasi ke startup entry yang tervalidasi sebagai Protected** — validasi di level service, bukan cuma UI
6. **TIDAK ADA Remove startup entry tanpa snapshot recovery tersimpan duluan** — reversible-by-design

---

## Rencana Selanjutnya
- User masih perlu **manual verification** Recovery System (restore, konflik path, purge) yang belum sempat dilakukan.
- User juga perlu **manual verification** Startup Manager: scan di mesin, cocokkan dengan Task Manager > Startup tab

---

## Development Rules
- `dotnet test` harus selalu hijau semua sebelum lanjut fase
- Semua cleanup WAJIB lewat pipeline `ICleanupExecutor` (yang otomatis integrate Recovery System)
- Jangan buat alur delete terpisah di luar pipeline yang ada
- Pakai `InternalsVisibleTo` di Infrastructure csproj supaya test bisa akses internal types
- App WAJIB jalan sebagai Administrator (auto-elevate via UAC, jangan di-skip)
