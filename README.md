# 🧹 PurgeAppCompat (PowerShell Edition)

> A powerful PowerShell script to **aggressively disable** legacy Application Compatibility features on Windows 11.

[![PowerShell](https://img.shields.io/badge/PowerShell-5.1%2B-blue)](https://github.com/PowerShell/PowerShell)
[![Platform](https://img.shields.io/badge/Platform-Windows%2011-blue)](https://www.microsoft.com/windows/windows-11)

---

## ✨ What It Does

PurgeAppCompat helps you completely disable **legacy Application Compatibility** mechanisms in Windows 11 — features that have remained since Windows Vista/7/8 era.

### Available Purge Levels

| Level | Name                        | Description                                           | Risk Level |
|-------|-----------------------------|-------------------------------------------------------|------------|
| **1** | 🔥 Complete Purge           | Maximum aggressive cleanup (recommended for power users) | 🔴 High    |
| **2** | 🛡️ Safe Recommended Purge   | Recommended safe option for most users               | 🟢 Low     |
| **3** | ↩️ Restore Defaults         | Revert all changes made by the script                | 🟡 Medium  |

---

## 🚀 Features

- ✅ Creates **System Restore Point** before dangerous operations
- ✅ Disables **Program Compatibility Assistant** service
- ✅ Applies strict compatibility policies
- ✅ Disables **Application Experience** scheduled tasks
- ✅ Completely clears **Compatibility Layers** from the registry
- ✅ Beautiful interactive arrow-key menu
- ✅ Detailed logging of all actions

---

## ⚠️ Important Warning

> **Level 1** is a very aggressive operation.  
> After running it, some very old applications may stop working correctly.

Always back up important data before using Level 1.

---

## 📥 Usage

1. Download the latest version:
   - [PurgeAppCompat.ps1](https://raw.githubusercontent.com/Mitroshenkov87/PurgeAppCompat-PWSH/main/PurgeAppCompat.ps1)

2. Run **PowerShell as Administrator**

3. Unblock the script (if needed):

```powershell
Unblock-File -Path .\PurgeAppCompat.ps1
```

4. Run it:

```powershell
.\PurgeAppCompat.ps1
```

Or run specific level directly:

```powershell
.\PurgeAppCompat.ps1 -Level 1
```

---

## 🛡️ Safety

- The script always tries to create a System Restore Point before Level 1
- Multiple confirmation prompts before dangerous actions
- All actions are logged in detail

---

## 📄 License

MIT License

---

**PurgeAppCompat-PWSH** — For those who want a truly clean Windows 11 without legacy ballast. 🔥
