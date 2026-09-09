using System.IO;
using System.Net.Http;
using System.Text.Json;
using GameBotMonitor.Models;

namespace GameBotMonitor.Services;

public class TelegramBotListener
{
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(35) };
    private CancellationTokenSource? _cts;
    private Task? _pollingTask;
    private long _lastUpdateId = 0;

    // Delegates / Callbacks ke MainWindow
    public Func<Task<string>>? OnGetStatus { get; set; }
    public Func<Task<string?>>? OnTakeScreenshot { get; set; }
    public Func<Task<bool>>? OnTileWindows { get; set; }
    public Func<bool, Task<string>>? OnToggleMonitoring { get; set; }
    public Func<int, Task<(bool success, string? imagePath, string message)>>? OnCheckBackpack { get; set; }
    public Func<int, int, Task<(bool success, string? imagePath, string message)>>? OnSwitchCharacter { get; set; }
    public Func<int, bool, Task<string>>? OnToggleSlot { get; set; }
    public Func<int, bool, Task<string>>? OnToggleAutoTab { get; set; }
    public Action<string>? OnLog { get; set; }

    public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;

    public void Start(AppConfig config)
    {
        if (IsRunning) return;

        if (!config.Telegram.Enabled || !config.Telegram.InteractiveEnabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(config.Telegram.BotToken) || string.IsNullOrWhiteSpace(config.Telegram.ChatId))
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _pollingTask = Task.Run(() => PollLoopAsync(config, _cts.Token));
        OnLog?.Invoke("[INFO] Telegram Remote Control (2-Way) aktif & mendengarkan perintah dari HP.");
    }

    public void Stop()
    {
        try
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
        catch { }
        finally
        {
            _cts = null;
            _pollingTask = null;
        }
        OnLog?.Invoke("[INFO] Telegram Remote Control (2-Way) dihentikan.");
    }

    private async Task PollLoopAsync(AppConfig config, CancellationToken ct)
    {
        string token = config.Telegram.BotToken.Trim();
        string authorizedChatId = config.Telegram.ChatId.Trim();

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var url = $"https://api.telegram.org/bot{token}/getUpdates?offset={_lastUpdateId + 1}&timeout=20";
                var response = await _httpClient.GetAsync(url, ct);

                if (!response.IsSuccessStatusCode)
                {
                    await Task.Delay(3000, ct);
                    continue;
                }

                var jsonStr = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(jsonStr);
                var root = doc.RootElement;

                if (root.TryGetProperty("ok", out var okProp) && okProp.GetBoolean())
                {
                    if (root.TryGetProperty("result", out var results) && results.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in results.EnumerateArray())
                        {
                            if (item.TryGetProperty("update_id", out var updateIdProp))
                            {
                                _lastUpdateId = updateIdProp.GetInt64();
                            }

                            if (item.TryGetProperty("message", out var msg))
                            {
                                await ProcessMessageAsync(msg, token, authorizedChatId);
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TelegramListener] Error: {ex.Message}");
                try
                {
                    await Task.Delay(3000, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task ProcessMessageAsync(JsonElement msg, string token, string authorizedChatId)
    {
        try
        {
            // Ambil chatId pengirim
            if (!msg.TryGetProperty("chat", out var chat) || !chat.TryGetProperty("id", out var chatIdProp))
            {
                return;
            }

            string incomingChatId = chatIdProp.ToString();

            // Keamanan: Tolak jika bukan dari Chat ID terdaftar
            if (incomingChatId != authorizedChatId)
            {
                OnLog?.Invoke($"[WARN] Telegram menerima pesan dari Chat ID tidak dikenal ({incomingChatId}). Diabaikan demi keamanan.");
                return;
            }

            if (!msg.TryGetProperty("text", out var textProp))
            {
                return;
            }

            string text = textProp.GetString()?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(text)) return;

            OnLog?.Invoke($"[REMOTE] Telegram command diterima: '{text}'");

            // Normalisasi perintah
            string cleanCmd = text.Split(' ')[0].ToLowerInvariant();
            // Hapus @botusername jika ada (misal /status@MyBot)
            if (cleanCmd.Contains('@'))
            {
                cleanCmd = cleanCmd.Split('@')[0];
            }

            switch (cleanCmd)
            {
                case "/help":
                case "help":
                case "/bantuan":
                    await SendHelpAsync(token, authorizedChatId);
                    break;

                case "/status":
                case "status":
                    await HandleStatusAsync(token, authorizedChatId);
                    break;

                case "/ss":
                case "/screenshot":
                case "ss":
                    await HandleScreenshotAsync(token, authorizedChatId);
                    break;

                case "/tile":
                case "tile":
                    await HandleTileAsync(token, authorizedChatId);
                    break;

                case "/start":
                case "/mulai":
                    await HandleToggleMonitoringAsync(token, authorizedChatId, true);
                    break;

                case "/stop":
                case "/berhenti":
                    await HandleToggleMonitoringAsync(token, authorizedChatId, false);
                    break;

                // --- CEK TAS (Support format tanpa spasi: /bag1, /bag2, /bag3 dll) ---
                case "/bag1":
                case "bag1":
                case "/cektas1":
                case "/tas1":
                    await HandleCheckBackpackAsync(token, authorizedChatId, 1);
                    break;

                case "/bag2":
                case "bag2":
                case "/cektas2":
                case "/tas2":
                    await HandleCheckBackpackAsync(token, authorizedChatId, 2);
                    break;

                case "/bag3":
                case "bag3":
                case "/cektas3":
                case "/tas3":
                    await HandleCheckBackpackAsync(token, authorizedChatId, 3);
                    break;

                case "/allbag":
                case "allbag":
                case "/cektasall":
                case "/bagall":
                    await HandleCheckAllBackpacksAsync(token, authorizedChatId);
                    break;

                case "/cektas":
                case "cektas":
                case "/tas":
                case "tas":
                case "/bag":
                case "bag":
                    int slotNum = 1;
                    var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 1 && int.TryParse(parts[1], out int parsedSlot))
                    {
                        slotNum = Math.Clamp(parsedSlot, 1, 3);
                    }
                    await HandleCheckBackpackAsync(token, authorizedChatId, slotNum);
                    break;

                // --- GANTI KARAKTER (CHARACTER SWITCHER) ---
                case "/char":
                case "char":
                case "/gantichar":
                case "gantichar":
                    var charParts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    int sNum = 1;
                    int cNum = 1;
                    if (charParts.Length >= 3)
                    {
                        int.TryParse(charParts[1], out sNum);
                        int.TryParse(charParts[2], out cNum);
                    }
                    else if (charParts.Length == 2)
                    {
                        int.TryParse(charParts[1], out cNum);
                    }
                    sNum = Math.Clamp(sNum, 1, 3);
                    cNum = Math.Clamp(cNum, 1, 9);
                    await HandleSwitchCharacterAsync(token, authorizedChatId, sNum, cNum);
                    break;

                case "/char1":
                case "char1":
                case "/c1":
                case "c1":
                    int c1 = 1;
                    var c1Parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (c1Parts.Length > 1 && int.TryParse(c1Parts[1], out int p1)) c1 = p1;
                    await HandleSwitchCharacterAsync(token, authorizedChatId, 1, Math.Clamp(c1, 1, 9));
                    break;

                case "/char2":
                case "char2":
                case "/c2":
                case "c2":
                    int c2 = 1;
                    var c2Parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (c2Parts.Length > 1 && int.TryParse(c2Parts[1], out int p2)) c2 = p2;
                    await HandleSwitchCharacterAsync(token, authorizedChatId, 2, Math.Clamp(c2, 1, 9));
                    break;

                case "/char3":
                case "char3":
                case "/c3":
                case "c3":
                    int c3 = 1;
                    var c3Parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (c3Parts.Length > 1 && int.TryParse(c3Parts[1], out int p3)) c3 = p3;
                    await HandleSwitchCharacterAsync(token, authorizedChatId, 3, Math.Clamp(c3, 1, 9));
                    break;

                // --- ALL SLOTS ON / OFF ---
                case "/allsloton":
                case "allsloton":
                case "/allon":
                case "allon":
                    await HandleToggleAllSlotsAsync(token, authorizedChatId, true);
                    break;

                case "/allslotoff":
                case "allslotoff":
                case "/alloff":
                case "alloff":
                    await HandleToggleAllSlotsAsync(token, authorizedChatId, false);
                    break;

                // --- ALL AUTO TAB ON / OFF ---
                case "/alltabon":
                case "alltabon":
                    await HandleToggleAllAutoTabAsync(token, authorizedChatId, true);
                    break;

                case "/alltaboff":
                case "alltaboff":
                    await HandleToggleAllAutoTabAsync(token, authorizedChatId, false);
                    break;

                // --- SLOT MONITORING ON / OFF PER SLOT ---
                case "/slot1on":
                case "slot1on":
                case "/on1":
                case "on1":
                    await HandleToggleSlotAsync(token, authorizedChatId, 1, true);
                    break;
                case "/slot1off":
                case "slot1off":
                case "/off1":
                case "off1":
                    await HandleToggleSlotAsync(token, authorizedChatId, 1, false);
                    break;

                case "/slot2on":
                case "slot2on":
                case "/on2":
                case "on2":
                    await HandleToggleSlotAsync(token, authorizedChatId, 2, true);
                    break;
                case "/slot2off":
                case "slot2off":
                case "/off2":
                case "off2":
                    await HandleToggleSlotAsync(token, authorizedChatId, 2, false);
                    break;

                case "/slot3on":
                case "slot3on":
                case "/on3":
                case "on3":
                    await HandleToggleSlotAsync(token, authorizedChatId, 3, true);
                    break;
                case "/slot3off":
                case "slot3off":
                case "/off3":
                case "off3":
                    await HandleToggleSlotAsync(token, authorizedChatId, 3, false);
                    break;

                // --- AUTO TAB ON / OFF PER SLOT ---
                case "/tab1on":
                case "tab1on":
                    await HandleToggleAutoTabAsync(token, authorizedChatId, 1, true);
                    break;
                case "/tab1off":
                case "tab1off":
                    await HandleToggleAutoTabAsync(token, authorizedChatId, 1, false);
                    break;

                case "/tab2on":
                case "tab2on":
                    await HandleToggleAutoTabAsync(token, authorizedChatId, 2, true);
                    break;
                case "/tab2off":
                case "tab2off":
                    await HandleToggleAutoTabAsync(token, authorizedChatId, 2, false);
                    break;

                case "/tab3on":
                case "tab3on":
                    await HandleToggleAutoTabAsync(token, authorizedChatId, 3, true);
                    break;
                case "/tab3off":
                case "tab3off":
                    await HandleToggleAutoTabAsync(token, authorizedChatId, 3, false);
                    break;

                default:
                    await TelegramNotifier.SendMessageAsync(
                        token,
                        authorizedChatId,
                        $"❓ Perintah `{text}` tidak dikenali.\nKetik `/help` untuk melihat daftar perintah yang tersedia.");
                    break;
            }
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"[ERROR] Telegram processing error: {ex.Message}");
        }
    }

    private async Task SendHelpAsync(string token, string chatId)
    {
        string helpMsg = 
            "🤖 *GameBotMonitor — Remote Control Commands*\n\n" +
            "📊 `/status` - Cek status realtime ketiga slot (termasuk status TAB)\n" +
            "📸 `/ss` - Ambil live screenshot layar PC\n" +
            "📐 `/tile` - Rapikan susunan 3 jendela game\n" +
            "▶️ `/start` / ⏹️ `/stop` - Nyalakan/matikan pemantauan bot\n\n" +
            "🎒 *Cek Tas & Gold:*\n" +
            "• `/allbag` - Cek tas SEMUA slot (1, 2, 3)\n" +
            "• `/bag1`, `/bag2`, `/bag3` - Cek tas per slot\n\n" +
            "🔔 *Monitoring Notifikasi Slot:*\n" +
            "• `/allsloton` / `/allslotoff` - ON/OFF SEMUA slot\n" +
            "• `/slot1on` / `/slot1off` - ON/OFF Slot 1\n" +
            "• `/slot2on` / `/slot2off` - ON/OFF Slot 2\n" +
            "• `/slot3on` / `/slot3off` - ON/OFF Slot 3\n\n" +
            "🎯 *Auto Assist TAB:*\n" +
            "• `/alltabon` / `/alltaboff` - ON/OFF Auto TAB SEMUA slot\n" +
            "• `/tab1on` / `/tab1off` - ON/OFF Auto TAB Slot 1\n" +
            "• `/tab2on` / `/tab2off` - ON/OFF Auto TAB Slot 2\n" +
            "• `/tab3on` / `/tab3off` - ON/OFF Auto TAB Slot 3\n\n" +
            "🔄 *Ganti Karakter (Remote Switch):*\n" +
            "• `/char [slot] [nomor]` - Ganti char (contoh: `/char 1 2`)\n" +
            "• `/char1 [nomor]` - Ganti char Slot 1 (contoh: `/char1 2`)\n" +
            "• `/char2 [nomor]` - Ganti char Slot 2 (contoh: `/char2 1`)\n" +
            "• `/char3 [nomor]` - Ganti char Slot 3 (contoh: `/char3 3`)";

        await TelegramNotifier.SendMessageAsync(token, chatId, helpMsg);
    }

    private async Task HandleStatusAsync(string token, string chatId)
    {
        if (OnGetStatus != null)
        {
            string statusReport = await OnGetStatus.Invoke();
            await TelegramNotifier.SendMessageAsync(token, chatId, statusReport);
        }
    }

    private async Task HandleScreenshotAsync(string token, string chatId)
    {
        await TelegramNotifier.SendMessageAsync(token, chatId, "📸 Sedang mengambil screenshot layar PC...");

        if (OnTakeScreenshot != null)
        {
            string? imagePath = await OnTakeScreenshot.Invoke();
            if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
            {
                string caption = $"📸 Live Screenshot PC ({DateTime.Now:HH:mm:ss})";
                await TelegramNotifier.SendPhotoDirectAsync(token, chatId, imagePath, caption);
            }
            else
            {
                await TelegramNotifier.SendMessageAsync(token, chatId, "❌ Gagal mengambil screenshot layar.");
            }
        }
    }

    private async Task HandleTileAsync(string token, string chatId)
    {
        if (OnTileWindows != null)
        {
            bool ok = await OnTileWindows.Invoke();
            string msg = ok 
                ? "📐 Jendela game berhasil dirapikan ke susunan 1-2-3." 
                : "⚠️ Tidak ditemukan jendela game untuk dirapikan.";
            await TelegramNotifier.SendMessageAsync(token, chatId, msg);
        }
    }

    private async Task HandleToggleMonitoringAsync(string token, string chatId, bool start)
    {
        if (OnToggleMonitoring != null)
        {
            string result = await OnToggleMonitoring.Invoke(start);
            await TelegramNotifier.SendMessageAsync(token, chatId, result);
        }
    }

    private async Task HandleCheckBackpackAsync(string token, string chatId, int slotNum)
    {
        await TelegramNotifier.SendMessageAsync(token, chatId, $"🎒 Memproses pemeriksaan tas untuk Slot {slotNum}...");

        if (OnCheckBackpack != null)
        {
            var (success, imagePath, message) = await OnCheckBackpack.Invoke(slotNum);
            if (success && !string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
            {
                await TelegramNotifier.SendPhotoDirectAsync(token, chatId, imagePath, message);
            }
            else
            {
                await TelegramNotifier.SendMessageAsync(token, chatId, message);
            }
        }
        else
        {
            await TelegramNotifier.SendMessageAsync(token, chatId, "⚠️ Handler Cek Tas belum siap.");
        }
    }

    private async Task HandleToggleSlotAsync(string token, string chatId, int slotNum, bool enable)
    {
        if (OnToggleSlot != null)
        {
            string result = await OnToggleSlot.Invoke(slotNum, enable);
            await TelegramNotifier.SendMessageAsync(token, chatId, result);
        }
        else
        {
            await TelegramNotifier.SendMessageAsync(token, chatId, "⚠️ Handler Slot belum siap.");
        }
    }

    private async Task HandleToggleAutoTabAsync(string token, string chatId, int slotNum, bool enable)
    {
        if (OnToggleAutoTab != null)
        {
            string result = await OnToggleAutoTab.Invoke(slotNum, enable);
            await TelegramNotifier.SendMessageAsync(token, chatId, result);
        }
        else
        {
            await TelegramNotifier.SendMessageAsync(token, chatId, "⚠️ Handler Auto TAB belum siap.");
        }
    }

    private async Task HandleCheckAllBackpacksAsync(string token, string chatId)
    {
        await TelegramNotifier.SendMessageAsync(token, chatId, "🎒 Memproses pemeriksaan tas untuk SEMUA slot (Slot 1, 2, 3)...");

        for (int slot = 1; slot <= 3; slot++)
        {
            if (OnCheckBackpack != null)
            {
                var (success, imagePath, message) = await OnCheckBackpack.Invoke(slot);
                if (success && !string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
                {
                    await TelegramNotifier.SendPhotoDirectAsync(token, chatId, imagePath, message);
                }
                else
                {
                    await TelegramNotifier.SendMessageAsync(token, chatId, message);
                }
            }
            if (slot < 3)
            {
                await Task.Delay(1000);
            }
        }
    }

    private async Task HandleToggleAllSlotsAsync(string token, string chatId, bool enable)
    {
        if (OnToggleSlot != null)
        {
            await OnToggleSlot.Invoke(1, enable);
            await OnToggleSlot.Invoke(2, enable);
            await OnToggleSlot.Invoke(3, enable);

            string status = enable ? "🟢 DIAKTIFKAN (ON)" : "⚪ DINONAKTIFKAN (OFF)";
            await TelegramNotifier.SendMessageAsync(token, chatId, $"🔔 Monitoring SEMUA Slot (1, 2, 3) berhasil {status}.");
        }
        else
        {
            await TelegramNotifier.SendMessageAsync(token, chatId, "⚠️ Handler Slot belum siap.");
        }
    }

    private async Task HandleToggleAllAutoTabAsync(string token, string chatId, bool enable)
    {
        if (OnToggleAutoTab != null)
        {
            await OnToggleAutoTab.Invoke(1, enable);
            await OnToggleAutoTab.Invoke(2, enable);
            await OnToggleAutoTab.Invoke(3, enable);

            string status = enable ? "🟢 DIAKTIFKAN (ON)" : "⚪ DINONAKTIFKAN (OFF)";
            await TelegramNotifier.SendMessageAsync(token, chatId, $"🎯 Auto TAB SEMUA Slot (1, 2, 3) berhasil {status}.");
        }
        else
        {
            await TelegramNotifier.SendMessageAsync(token, chatId, "⚠️ Handler Auto TAB belum siap.");
        }
    }

    private async Task HandleSwitchCharacterAsync(string token, string chatId, int slotNum, int targetCharNum)
    {
        await TelegramNotifier.SendMessageAsync(
            token, 
            chatId, 
            $"🔄 Memulai proses pergantian karakter untuk *Slot {slotNum}* ke *Karakter {targetCharNum}*...\nMohon tunggu proses logout, seleksi, & loading in-game (~10-15 detik).");

        if (OnSwitchCharacter != null)
        {
            var (success, imagePath, message) = await OnSwitchCharacter.Invoke(slotNum, targetCharNum);
            if (success && !string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
            {
                await TelegramNotifier.SendPhotoDirectAsync(token, chatId, imagePath, message);
            }
            else
            {
                await TelegramNotifier.SendMessageAsync(token, chatId, message);
            }
        }
        else
        {
            await TelegramNotifier.SendMessageAsync(token, chatId, "⚠️ Handler Ganti Karakter belum siap.");
        }
    }
}
