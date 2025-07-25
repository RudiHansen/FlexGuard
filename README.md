# 🛡️ FlexGuard

**A modular, efficient, and version-aware backup tool for modern workflows.**

---

## 📖 Why FlexGuard?

FlexGuard was created to address the limitations of traditional backup tools. Existing solutions often:
- Duplicate large amounts of unchanged data, wasting storage and time.
- Provide limited control over compression and file grouping.
- Lack clear versioning and the ability to restore older file states efficiently.
- Fail to handle ransomware resilience or WORM-like protection.
- Do not integrate well with modern multi-destination setups (NAS, cloud, USB).

**FlexGuard aims to deliver:**
- Efficient full and differential backups.
- Fine-grained file versioning and restore capabilities.
- Modern compression (Zstd, Brotli, GZip) with chunk-based storage.
- Minimal memory usage via streaming and temp files.
- Modular architecture that can grow with future requirements.

---

## ✨ Features

- **Full & Differential Backups:** Full backups monthly, differential backups daily.
- **Chunk-Based Storage:** Files grouped into compressed chunks (.fgchunk).
- **Compression Options:** GZip, Brotli, Zstd (Zstd recommended for best speed).
- **SHA-256 Hashing:** Data integrity checks for every file.
- **Low Memory Usage:** <100 MB memory footprint thanks to streaming.
- **Interactive Restore Selector:** Directory/Tree views, filtering, select-all, and clear-all.
- **Logging & Performance Metrics:** BackupRegistry and PerfLogger for detailed job statistics.
- **Pluggable Architecture:** Replaceable components for compression, logging, and grouping.

---

## 🏗️ Project Structure

FlexGuard is organized into multiple projects:

- **FlexGuard.Core** – Core backup and restore logic (chunking, compression, hashing, manifests).
- **FlexGuard.CLI** – Command-line interface, argument parsing, and interactive restore selector.
- **FlexGuard.Benchmark** – Performance and compression benchmark utilities.
- **FlexGuard.UI** *(planned)* – Windows Forms-based UI for job management and restores.
- **FlexGuard.Tests** *(planned)* – Unit and integration tests.

---

## 🚀 Quick Start

### Requirements
- **.NET 8 SDK** or newer.
- Windows (Linux support planned in future versions).

### Installation
Clone the repository and build with .NET:
```bash
git clone https://github.com/RudiHansen/FlexGuard.git
cd FlexGuard
dotnet build
```

Or download the pre-built binary from the [Releases](https://github.com/RudiHansen/FlexGuard/releases) page (planned).

### Example Usage
Perform a full backup:
```bash
flexguard --jobname "MyBackup" --mode full --compression zstd
```

Restore selected files:
```bash
flexguard --jobname "MyBackup" --mode restore
```

Show help:
```bash
flexguard --help
```

---

## ⚙️ Configuration

FlexGuard uses a JSON-based job configuration file (`job_default.json`) to define backup sources, destinations, and restore targets.  
Copy `job_default.json` to create your own job file, e.g.:
```bash
cp job_default.json my_job.json
```

**Fields:**
- **JobName:** Unique name for the backup job (e.g., "PhotosBackup").
- **Sources:** A list of source folders to include in the backup.
  - **Path:** Absolute or relative path to the folder to back up.
  - **Exclude:** A list of patterns or folder names to skip (e.g., ["*.tmp", "bin", "obj"]).
- **DestinationPath:** The directory where backup chunks, manifests, and logs will be stored.
- **RestoreTargetFolder:** When performing a restore, this folder is where selected files will be restored.

**Example:**
```json
{
  "JobName": "default",
  "Sources": [
    {
      "Path": "C:/Temp",
      "Exclude": [ "*.tmp", "bin", "obj" ]
    },
    {
      "Path": "D:/Projects",
      "Exclude": [ "bin", "obj" ]
    }
  ],
  "DestinationPath": "E:/Backups/FlexGuard",
  "RestoreTargetFolder": "C:/RestoredFiles"
}
```

---

## ⚙️ CLI Options

| Argument                | Description                                    |
|-------------------------|------------------------------------------------|
| `--jobname <name>`      | Specifies the backup job name.                  |
| `--mode <full\|diff\|restore>` | Backup mode: Full, Differential, or Restore. |
| `--maxfiles <number>`   | Max number of files per chunk group. (Default: 1000)|
| `--maxbytes <size>`     | Max total size per chunk group. (Default: 1GB)  |
| `--compression <method>`| Compression method: gzip, brotli, zstd. (Default Zstd) |
| `--measure-compression` | Planned feature for compression ratio logging.  |
| `-v` or `--version`     | Displays the current version.                   |
| `-h` or `--help`        | Displays help information.                      |

---

## 📦 Roadmap

- **v0.4 (Planned):**
  - Diff-based storage for file versioning.
  - Hash-per-chunk and optional manifest signing.
  - Transaction-safe manifest writes.
  - Expanded test coverage.

- **Future:**
  - SCP/SSH and cloud targets (OneDrive, S3).
  - UI layer (Windows Forms or alternative).
  - Retention and pruning policies.
  - WORM-like backup protection.

---

## 🧪 Development & Contribution

FlexGuard is currently in **beta (v0.3)**. Contributions, bug reports, and suggestions are welcome.

1. Fork the repo.
2. Create a feature branch.
3. Submit a PR with detailed description and tests.

---

## 📜 License

This project is licensed under the MIT License – see [LICENSE](LICENSE) for details.

---

## 📞 Contact

Developed by **Rudi Stensborg Hansen**.  
For issues and feedback, please open a [GitHub Issue](https://github.com/RudiHansen/FlexGuard/issues).
