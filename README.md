# PurgeAppCompat v2.2

**Advanced PowerShell tool to purge legacy Application Compatibility features from Windows 11.**

Removes unnecessary compatibility infrastructure (PCA service + policy, scheduled tasks, and registry layers) for users who only run modern software.

> **Note:** Starting from v2.2 we **no longer touch** `sysmain.sdb`. This file is heavily protected by TrustedInstaller and SFC. Disabling the other compatibility components already gives excellent results without the risk of the file being restored or causing side effects.

## ⚠️ Warning

- Level 1 is aggressive. It disables core compatibility mechanisms.
- Always create a **System Restore point** before using Level 1 (script does this by default).
- Intended primarily for **Windows 11** power users.

## Features

- Disables **Program Compatibility Assistant (PCA)** service and policy
- Disables **Application Experience** scheduled tasks
- Clears old compatibility **Layers** from registry
- Automatic **backups** + **System Restore point**
- Beautiful interactive arrow-key menu
- Full logging
- Safe restore mode (Level 3)
- Does **not** touch the protected `sysmain.sdb` file

## Usage

### Interactive Mode (Recommended)

```powershell
.\PurgeAppCompat.ps1
```

Use ↑↓ arrows and Enter.

### Non-Interactive

```powershell
# Safe recommended purge
.\PurgeAppCompat.ps1 -Level 2

# Full aggressive purge
.\PurgeAppCompat.ps1 -Level 1 -Force
```

## Parameters

| Parameter         | Description                                      |
|-------------------|--------------------------------------------------|
| `-Level`          | `1` = Complete, `2` = Safe (default), `3` = Restore |
| `-Force`          | Skip confirmations                               |
| `-NoRestorePoint` | Skip System Restore point                        |

## Levels

| Level | Name              | Description                                      | Risk  |
|-------|-------------------|--------------------------------------------------|-------|
| 1     | Complete Purge    | Disables PCA + tasks + clears registry layers    | Medium|
| 2     | Recommended Safe  | Same as Level 1 but with confirmations           | Low   |
| 3     | Restore Defaults  | Re-enables everything                            | None  |

## Requirements

- Windows 11 (best)
- PowerShell 5.1+
- Administrator rights

## Backups

All backups are saved to `C:\AppCompatBackups\`:

- Registry exports

## Why PurgeAppCompat?

Windows 11 still carries a lot of compatibility code from the Windows 7/Vista era. For users running only modern applications, this legacy layer is unnecessary overhead.

PurgeAppCompat gives you clean control without touching heavily protected system files like `sysmain.sdb`.

## Author & Credits

Created with ❤️ by **Grok** for **Aleksandr Mitroshenkov** (@Mitroshenkov87)

## License

MIT License

---

**Use responsibly.** This tool modifies core Windows compatibility mechanisms.