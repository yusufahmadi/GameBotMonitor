# 🚨 ATURAN PENTING: Encoding File XAML & CS di Project Ini

## MASALAH YANG PERNAH TERJADI (MOJIBAKE)

Teks emoji dan Unicode pernah rusak karena AI agent menulis ulang file tanpa preserving encoding UTF-8.

**Contoh kerusakan:**
```
BEFORE (benar):
<ComboBoxItem Content="🇮🇩 Bahasa Indonesia (ID)" Tag="id"/>
<TextBlock x:Name="SecLanguageHeader" Text="🌐 Bahasa / Language" .../>

AFTER (rusak - MOJIBAKE):
<ComboBoxItem Content="Ã°Å¸â€¡Â®Ã°Å¸â€¡Â© Bahasa Indonesia (ID)" Tag="id"/>
<TextBlock x:Name="SecLanguageHeader" Text="Ã°Å¸Å'Â Bahasa / Language" .../>
```

---

## ⚠️ ATURAN WAJIB UNTUK AI AGENT

### 1. JANGAN gunakan PowerShell -replace / Set-Content biasa untuk file yang berisi emoji
```powershell
# ❌ SALAH - Menyebabkan mojibake:
(Get-Content file.xaml) -replace "old", "new" | Set-Content file.xaml

# ✅ BENAR - Gunakan tools replace_file_content bawaan agent
```

### 2. Jika HARUS menulis file via PowerShell, SELALU gunakan UTF-8 dengan BOM
```powershell
# ✅ BENAR - Baca:
$content = [System.IO.File]::ReadAllText("file.xaml", [System.Text.UTF8Encoding]::new($true))

# ✅ BENAR - Tulis:
$utf8Bom = [System.Text.UTF8Encoding]::new($true)
[System.IO.File]::WriteAllText("file.xaml", $content, $utf8Bom)

# ❌ SALAH: Get-Content / Set-Content default mungkin pakai ANSI/CP1252
```

### 3. Gunakan replace_file_content tool (bukan PowerShell) untuk edit XAML
Tool replace_file_content sudah handle encoding dengan benar secara internal.

### 4. Verifikasi setelah setiap edit XAML
```powershell
& "C:\Program Files\dotnet\dotnet.exe" build "GameBotMonitor.csproj" -c Release
# HARUS: 0 Warning(s), 0 Error(s)
```

---

## 📋 DAFTAR EMOJI YANG DIGUNAKAN DI MAINWINDOW.XAML

| Emoji | Unicode | Lokasi |
|-------|---------|--------|
| 🎯 | U+1F3AF | Tombol kalibrasi, label Auto TAB |
| 🎮 | U+1F3AE | SecGameTargetHeader |
| ⚡ | U+26A1 | Tombol Test Tab |
| ⏱️ | U+23F1+FE0F | SecDetectionHeader |
| 🛠️ | U+1F6E0+FE0F | SecDiagnosticsHeader |
| 🏰 | U+1F3F0 | SML section header |
| 💾 | U+1F4BE | BtnSaveSettings |
| ◀ | U+25C0 | Tombol Prev |
| ▶ | U+25B6 | Tombol Next/Run |
| 👁️ | U+1F441+FE0F | BtnShowCharCalibrationOverlay |
| 🌐 | U+1F310 | SecLanguageHeader |
| 🇮🇩 | Sequence | ComboBox Indonesia |
| 🇬🇧 | Sequence | ComboBox English |
| • | U+2022 | TxtStorageInfo bullet |
| ○ | U+25CB | BtnAutoTabSlot OFF state |

---

## 🔧 CARA PUBLISH APLIKASI

```powershell
# Build check:
& "C:\Program Files\dotnet\dotnet.exe" build "c:\Research\GameBotMonitor\GameBotMonitor.csproj" -c Release

# Publish single-file:
taskkill /F /IM GameBotMonitor.exe 2>$null
& "C:\Program Files\dotnet\dotnet.exe" publish "c:\Research\GameBotMonitor\GameBotMonitor.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "c:\Research\GameBotMonitor\publish"
```

Output: `c:\Research\GameBotMonitor\publish\GameBotMonitor.exe`
