# 📜 FlexGuard Changelog

All notable changes to this project will be documented in this file.

---

## [Planned v0.4 / Next]
- Diff-based storage for file versioning (avoid full re-duplication).
- Hash per chunk file and optional manifest signing.
- Transaction-safe manifest writes (atomic/temporary file then swap).
- Expanded test coverage (unit + integration).
- **Fix System.Text.Json trimming warnings** by introducing `JsonSerializerContext` (source generators) or selectively disabling trimming for affected code paths to ensure `PublishTrimmed=true` works safely.

---

## [v0.3-beta] - Current
### Added
- Implemented CLI argument parsing (ProgramOptionsParser) with /?, /h, -h, --help.
- Added VersionHelper and centralized semantic versioning with Directory.Build.props (Git hash trimmed when displayed).
- MessageReporterConsole for consistent console/file logging with debug toggles.
- Enhanced RestoreFileSelector:
  * Directory and Tree View modes with persistent selections.
  * Filtering with case-insensitive contains.
  * Select-all and clear-all options.
  * Footer status showing selected/total files and directories.
  * Robust add/remove logic for directories (RemoveAllUnder / AddAllUnder).
- Verified restore logic with per-file hash validation.

### Improved
- Significant performance boost due to Zstd compression (over 60% faster than GZip).
- General stability improvements in chunk processing.

---

## [v0.2] - Stability & Performance
### Added
- Introduced Brotli and Zstd compressors, with Zstd as recommended default.
- Added BackupRegistry and manifest files with start/end timestamps to detect and resume/inspect interrupted backups.
- Copy of registry + manifest into backup folder for redundancy.
- Implemented PerfLogger for per-job statistics (JobName, Mode, Compression, FileCount, GroupCount, TotalTime).

### Changed
- Large cleanup of old and unused code.
- ChunkProcessor optimized with temporary files for low memory usage (< 100 MB).

---

## [v0.1] - Initial Prototype
### Added
- Basic structure for full and differential backups.
- First implementation of chunk-based storage.
- Initial GZip compression support.
