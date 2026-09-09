# 🎮 GameBotMonitor

> **⚠️ PEMBERITAHUAN PENTING (DISCLAIMER):**  
> **Source code aplikasi ini 100% dibuat dan digenerate oleh AI** (*Pair-programming assistant*). Mulai dari arsitektur backend .NET 8, deteksi pixel screen scanner, Win32 API window tracking, sistem notifikasi (Discord & Telegram), alarm audio, visual antarmuka WPF modern bertema gelap, hingga pembuatan multi-resolusi `.ico` aplikasi.

---

**GameBotMonitor** adalah aplikasi desktop Windows berbasis **.NET 8 WPF** berkinerja tinggi yang dirancang untuk memantau status kesehatan darah (HP Bar) karakter pada multi-klien bot game (seperti *Grand Fantasia Origin* atau MMORPG lainnya) secara otomatis, real-time, dan minim beban CPU/GPU.

Jika karakter mati (darah 0) atau klien game mengalami crash/freeze, aplikasi akan segera memberikan peringatan lokal (alarm suara WAV) serta mengirim notifikasi jarak jauh via **Discord Webhook** dan **Telegram Bot** lengkap dengan lampiran tangkapan layar (screenshot) HUD karakter.

---

## ✨ Fitur Utama

1. **⚡ Pemantauan Multi-Client Real-Time (Hingga 3 Klien)**
   - Mendeteksi jendela game secara otomatis berdasarkan judul jendela (*Window Title Target*).
   - Menampilkan slot pemantauan (SLOT 1, SLOT 2, SLOT 3) dengan informasi PID, status koneksi, dan sampel warna pixel hex.
   - Dilengkapi tombol toggle cepat per slot (**● ON** / **○ OFF**) jika Anda ingin menonaktifkan alert untuk slot tertentu secara terpisah.

2. **🖼️ Cuplikan Live HUD Karakter Sebagai Background Slot (Opsional)**
   - Tampilan latar belakang kartu slot dapat menampilkan live potongan HUD status karakter (avatar, nama, dan bar HP/MP).
   - Dilengkapi lapisan gradasi halus sehingga tulisan, status badge, dan tombol tetap kontras dan nyaman dibaca.
   - Sifatnya **opsional** (default nonaktif dengan gaya dark clean modern, dapat diaktifkan melalui menu Pengaturan).

3. **🔔 Notifikasi Instan Jarak Jauh (Discord & Telegram)**
   - **Discord Webhook**: Kirim alert embed rapi dengan thumbnail screenshot HUD, info slot, timestamp, status, serta opsi mention `@everyone`.
   - **Telegram Bot**: Kirim foto screenshot bukti kematian karakter langsung ke HP Anda via Telegram Bot API dengan tombol pengujian koneksi langsung di antarmuka.

4. **🔊 Alarm Lokal & Auto-Mute Saat Karakter Hidup Kembali**
   - Alarm suara berulang (sirine peringatan default atau file `.wav` kustom).
   - Tombol cepat **Stop Alarm** di toolbar.
   - Alarm otomatis berhenti berbunyi saat karakter terdeteksi hidup kembali (*auto-revived*).

5. **🌐 Dukungan Multi-Bahasa (Bilingual ID & EN)**
   - Tersedia pilihan **Bahasa Indonesia (ID)** dan **English (EN)**.
   - Berubah secara instan di seluruh tampilan, log, kartu slot, hingga isi pesan notifikasi Discord & Telegram tanpa perlu me-restart aplikasi.

6. **📐 Penataan Jendela Game Otomatis (Auto-Tile Windows)**
   - Tombol **"Rapikan Jendela / Tile Windows"** untuk menata posisi 3 jendela game secara berdampingan (Kiri, Tengah, Kanan).
   - Mempertahankan resolusi asli game tanpa mengubah ukuran jendela (*preserves game resolution*).

7. **🎯 Alat Kalibrasi Koordinat HP Bar**
   - Tombol **"Kalibrasi HP"** membuka overlay transparan interaktif untuk menentukan titik koordinat deteksi warna darah HP hanya dengan satu klik mouse.

8. **🛡️ Anti-False Alarm & Auto-Cleanup Penyimpanan**
   - **Delay Konfirmasi**: Memastikan darah benar-benar kosong selama beberapa detik sebelum memicu alarm (mencegah alarm palsu akibat animasi skill atau lag visual).
   - **Cooldown Notifikasi**: Menghindari spam alert beruntun.
   - **Auto-Cleanup**: Screenshot disimpan rapi per tanggal (`Capture/yyyy/MM/dd/`), dibatasi maksimal 10 gambar per hari, dan otomatis menghapus folder lebih dari 3 hari.

9. **🎯 Auto Assist Target (Auto TAB Otomatis)**
   - Mendeteksi ketiadaan target monster di area tengah atas layar via pixel sampling.
   - Jika dalam waktu X detik bot tidak memiliki target monster (idle/stuck), aplikasi otomatis mengirimkan assist penekanan tombol **`TAB`** untuk mengunci monster terdekat.
   - **Tombol On/Off Mandiri di Setiap Kartu Slot**: Anda dapat mengaktifkan Auto TAB untuk Slot 1 & 2 (penyerang) dan mematikannya untuk Slot 3 (support/healer).
   - **2 Mode Pengiriman Input**:
     - *Mode Background (`PostMessage`)*: Mengirim tombol langsung ke antrean jendela game tanpa mencuri fokus mouse atau mengganggu aktivitas Anda di PC.
     - *Mode Foreground (`SendInput`)*: Simulasi hardware event yang identik dengan keyboard fisik.
   - Tombol **"Kalibrasi Target"** untuk mencocokkan koordinat frame monster secara instan, serta tombol **"Test TAB Sekarang"**.

10. **📌 Dukungan Penuh System Tray Windows & UI Fleksibel**
    - Icon aplikasi dan icon system tray seragam menggunakan desain **Notification Bell**.
    - Tombol **"Simpan Semua Pengaturan"** dibuat *docked* di bagian bawah tab agar selalu terlihat dan langsung bisa diklik tanpa perlu scrolling.
    - Menutup jendela (tombol `X`) otomatis meminimalkan aplikasi ke System Tray di pojok kanan bawah agar pemantauan tetap berjalan di latar belakang.

11. **🎮 Remote Control Telegram Interaktif (2-Way)**
    - Aplikasi mendengarkan perintah langsung dari Telegram di smartphone Anda secara real-time via long-polling (tanpa perlu port forwarding).
    - **Aman (Security Whitelist)**: Hanya merespons dari `Chat ID` milik Anda yang terdaftar di Pengaturan.
    - **Daftar Perintah Lengkap**:
      - 📊 `/status` : Menampilkan ringkasan status realtime ketiga slot karakter.
      - 📸 `/ss` : Mengambil live screenshot layar desktop PC dan mengirim fotonya ke HP.
      - 📐 `/tile` : Merapikan susunan jendela game ke posisi berdampingan 1-2-3 dari jauh.
      - ▶️ `/start` / ⏹️ `/stop` : Menyalakan atau mematikan pemantauan bot.
      - 🎒 `/bag1`, `/bag2`, `/bag3` : Buka tas slot target (tombol `B`), foto sisa slot & total Gold, lalu tutup tas kembali secara otomatis.
      - 🔔 `/slot1on` / `/slot1off`, `/slot2on`, dll : Menyalakan / mematikan alert pemantauan per slot.
      - 🎯 `/tab1on` / `/tab1off`, `/tab2on`, dll : Menyalakan / mematikan Auto Assist TAB per slot.
      - ℹ️ `/help` : Menampilkan menu panduan perintah.

---

## 🖥️ Kebutuhan Sistem

- **Sistem Operasi**: Windows 10 / Windows 11 (64-bit)
- **Runtime**: Tidak perlu install .NET Runtime jika menggunakan versi *Self-Contained Single-File* (`publish/GameBotMonitor.exe`).
- **Resolusi Game**: Jendela game dapat berjalan dalam mode Windowed atau Borderless.

---

## 🚀 Cara Menjalankan

### Cara Cepat:
Cukup double-click file batch berikut di folder utama:
```bat
Jalankan_BotMonitor.bat
```
Atau langsung jalankan file mandiri hasil publish:
```
c:\Research\GameBotMonitor\publish\GameBotMonitor.exe
```

---

## ⚙️ Panduan Konfigurasi

Semua konfigurasi disimpan secara otomatis ke dalam file `config.json` di direktori yang sama dengan aplikasi. Anda dapat mengaturnya langsung dari tab **Pengaturan & Notifikasi (Settings & Notifications)**:

### 1. Deteksi & Delay
- **Pilih Bahasa**: `Bahasa Indonesia` atau `English`.
- **Interval Pindai Layar**: Frekuensi pengecekan pixel (rekomendasi: `1000` - `2000` ms).
- **Delay Konfirmasi**: Berapa lama HP harus tetap 0 sebelum notifikasi dikirim (default: `1.5` detik).
- **Cooldown Notifikasi**: Jeda waktu anti-spam sebelum alert berikutnya diizinkan (default: `3` menit).
- **Target Judul Window Game**: Kata kunci judul jendela game yang dicari (default: `Grand Fantasia Origin`).
- **Nama Game**: Nama game yang akan tampil pada judul aplikasi, embed Discord, dan pesan Telegram.
- **Cuplikan Info Karakter Sebagai Background**: Centang opsi ini jika ingin melihat visual live HUD karakter di kartu slot.

### 2. Pengaturan Discord Webhook
1. Di Discord, buka **Server Settings** > **Integrations** > **Webhooks** > **New Webhook**.
2. Salin URL Webhook dan tempelkan ke kolom **Discord Webhook URL**.
3. Centang opsi **Aktifkan Notifikasi Discord**.
4. Klik tombol **Test Discord** untuk memastikan pesan masuk ke channel Anda.

### 3. Pengaturan Telegram Bot
1. Buat bot baru di Telegram melalui **@BotFather** dengan perintah `/newbot`, ikuti petunjuk hingga mendapatkan **HTTP API Token** (contoh: `123456789:ABCdef...`).
2. Tempelkan token tersebut ke kolom **Telegram Bot Token**.
3. Untuk mendapatkan **Chat ID**:
   - Kirim pesan apa saja ke bot Anda terlebih dahulu di Telegram.
   - Buka bot **@userinfobot** di Telegram, atau buka URL berikut di browser:  
     `https://api.telegram.org/bot<TOKEN_ANDA>/getUpdates`
   - Cari angka pada kolom `"id"` di bawah objek `"chat"`.
4. Masukkan ID tersebut ke kolom **Chat ID Penerima**.
5. Centang opsi **Aktifkan Notifikasi Telegram** dan klik **Test Telegram**.

---

## 🛠️ Struktur Proyek

```text
GameBotMonitor/
├── app_icon.ico                 # Icon multi-resolusi aplikasi & system tray
├── App.xaml / App.xaml.cs       # Inisialisasi aplikasi WPF
├── MainWindow.xaml              # Desain antarmuka WPF (Dark Theme, Cards, Tabs)
├── MainWindow.xaml.cs           # Controller utama, timer scanner, tray handling
├── Jalankan_BotMonitor.bat      # Launcher shortcut
├── Models/
│   ├── AppConfig.cs             # Model konfigurasi aplikasi & penyimpanan json
│   ├── ClientSlot.cs            # State data per slot karakter
│   └── Enums.cs                 # Enum status klien & enum tipe notifikasi
├── Native/
│   └── Win32.cs                 # Interop P/Invoke API Windows (user32, gdi32)
├── Services/
│   ├── AutoTileService.cs       # Penataan otomatis multi-jendela game
│   ├── BackpackInspectorService.cs # Inspeksi tas game (tombol B, snapshot gold & slot)
│   ├── ConfigService.cs         # Serialisasi & deserialisasi config.json
│   ├── DiscordNotifier.cs       # Integrasi pengiriman Discord Webhook
│   ├── LanguageService.cs       # Layanan lokalisasi kamus bahasa ID & EN
│   ├── PixelHealthScanner.cs    # Algoritma sampling warna HP & screen capture
│   ├── SoundAlertService.cs     # Pemutar alarm audio lokal (WAV)
│   ├── StorageCleanupService.cs # Manajemen penyimpanan & rotasi folder screenshot
│   ├── TelegramBotListener.cs   # Background listener Telegram 2-arah (Remote Control)
│   ├── TelegramNotifier.cs      # Integrasi Telegram Bot API (SendPhoto / SendMessage)
│   └── WindowTrackerService.cs  # Pelacak handle jendela game aktif
├── Views/
│   └── CalibrationOverlay.xaml  # Jendela interaktif kalibrasi koordinat HP
└── publish/
    └── GameBotMonitor.exe       # Executable mandiri (Single-file release win-x64)
```

---

## 🤖 Catatan Pengembangan

- Seluruh kode C#, desain XAML, interop Windows Win32, algoritma deteksi, pembuatan asset icon, hingga perbaikan bug dan fitur multi-bahasa pada repositori ini **100% dibuat dan digenerate secara terstruktur oleh Artificial Intelligence (AI)** berdasarkan diskusi dan instruksi pengguna.
- Bebas dimodifikasi, dikembangkan, dan disesuaikan untuk berbagai jenis game MMORPG lainnya.