# PurgeAppCompat

A modern WinForms tool for aggressively disabling legacy Application Compatibility features on Windows 11.

## Features
- Level 1 (Nuclear): Hard purge of legacy compatibility (PCA service, policies, tasks, registry layers)
- Level 2 (Recommended): Safe recommended cleanup
- Level 3: Restore defaults
- Creates System Restore point before destructive operations
- Robust backup of important files before changes
- Clean, resizable interface with proper HiDPI support

## Requirements
- Windows 11 (recommended)
- .NET 10 Desktop Runtime (or build from source)
- Administrator privileges

## Building from source
```bash
dotnet build -c Release
```

The compiled executable will be in `bin\Release\net10.0-windows\`.

## Usage
Run `PurgeAppCompat.exe` as Administrator.

**Warning**: Level 1 makes permanent changes. Always review the log and consider creating a System Restore point manually if the automatic one fails.

## License
MIT (or whatever you choose)