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
    public Func<int, int, string, Task<(bool success, string? imagePath, string message)>>? OnEnterSml { get; set; }
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
                            else if (item.TryGetProperty("callback_query", out var cbQuery))
                            {
                                await ProcessCallbackQueryAsync(cbQuery, token, authorizedChatId);
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

            await ProcessCommandTextAsync(text, token, authorizedChatId);
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"[ERROR] Telegram processing error: {ex.Message}");
        }
    }

    private async Task ProcessCallbackQueryAsync(JsonElement cbQuery, string token, string authorizedChatId)
    {
        try
        {
            string queryId = cbQuery.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";

            string incomingChatId = "";
            long messageId = 0;
            if (cbQuery.TryGetProperty("message", out var msg))
            {
                if (msg.TryGetProperty("message_id", out var midProp))
                {
                    messageId = midProp.GetInt64();
                }
                if (msg.TryGetProperty("chat", out var chat) && chat.TryGetProperty("id", out var chatIdProp))
                {
                    incomingChatId = chatIdProp.ToString();
                }
            }

            if (incomingChatId != authorizedChatId)
            {
                OnLog?.Invoke($"[WARN] Telegram callback query dari Chat ID tidak dikenal ({incomingChatId}). Diabaikan demi keamanan.");
                return;
            }

            // Hentikan loading spinner di tombol Telegram pengguna
            if (!string.IsNullOrEmpty(queryId))
            {
                _ = TelegramNotifier.AnswerCallbackQueryAsync(token, queryId);
            }

            if (cbQuery.TryGetProperty("data", out var dataProp))
            {
                string data = dataProp.GetString()?.Trim() ?? "";
                if (!string.IsNullOrWhiteSpace(data))
                {
                    OnLog?.Invoke($"[REMOTE] Telegram inline button diklik: '{data}'");

                    if (data.StartsWith("menu_"))
                    {
                        await HandleMenuNavigationAsync(token, authorizedChatId, messageId, data);
                    }
                    else
                    {
                        await ProcessCommandTextAsync(data, token, authorizedChatId);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"[ERROR] Telegram CallbackQuery error: {ex.Message}");
        }
    }

    private async Task ProcessCommandTextAsync(string text, string token, string authorizedChatId)
    {
        try
        {
            OnLog?.Invoke($"[REMOTE] Telegram command diproses: '{text}'");

            // Normalisasi perintah
            string cleanCmd = text.Split(' ')[0].ToLowerInvariant();
            // Hapus @botusername jika ada (misal /status@MyBot)
            if (cleanCmd.Contains('@'))
            {
                cleanCmd = cleanCmd.Split('@')[0];
            }

            // Deteksi langsung perintah format sml_1_1_max atau sml1-1-max
            if (cleanCmd.StartsWith("sml_") || cleanCmd.StartsWith("/sml_") || cleanCmd.StartsWith("sml1-") || cleanCmd.StartsWith("sml2-") || cleanCmd.StartsWith("sml3-"))
            {
                cleanCmd = "sml";
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

                // --- CLEAR SML (SPRITE MAGIC LAND) ---
                // Format: /sml [slot] [lv] [floor] | sml_1_1_max | sml_1_1_90 | sml_1_1_-2 | /sml1 1 max
                case "/sml":
                case "sml":
                case "/clearsml":
                case "clearsml":
                    int smlSlot = 1;
                    int smlLv = 1;
                    string smlFloor = "max";

                    var smlParts = text.Split(new[] { ' ', '_' }, StringSplitOptions.RemoveEmptyEntries);
                    if (smlParts.Length >= 4)
                    {
                        int.TryParse(smlParts[1], out smlSlot);
                        int.TryParse(smlParts[2], out smlLv);
                        smlFloor = smlParts[3];
                    }
                    else if (smlParts.Length == 3)
                    {
                        int.TryParse(smlParts[1], out smlSlot);
                        int.TryParse(smlParts[2], out smlLv);
                    }
                    else if (smlParts.Length == 2)
                    {
                        int.TryParse(smlParts[1], out smlLv);
                    }

                    smlSlot = Math.Clamp(smlSlot, 1, 3);
                    smlLv = Math.Clamp(smlLv, 1, 4);
                    await HandleEnterSmlAsync(token, authorizedChatId, smlSlot, smlLv, smlFloor);
                    break;

                case "/sml1":
                case "sml1":
                case "/sml2":
                case "sml2":
                case "/sml3":
                case "sml3":
                    int targetSmlSlot = cleanCmd.Contains('2') ? 2 : (cleanCmd.Contains('3') ? 3 : 1);
                    int targetSmlLv = 1;
                    string targetSmlFloor = "max";

                    var sParts = text.Split(new[] { ' ', '_' }, StringSplitOptions.RemoveEmptyEntries);
                    if (sParts.Length >= 3)
                    {
                        int.TryParse(sParts[1], out targetSmlLv);
                        targetSmlFloor = sParts[2];
                    }
                    else if (sParts.Length == 2)
                    {
                        int.TryParse(sParts[1], out targetSmlLv);
                    }

                    targetSmlLv = Math.Clamp(targetSmlLv, 1, 4);
                    await HandleEnterSmlAsync(token, authorizedChatId, targetSmlSlot, targetSmlLv, targetSmlFloor);
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
        string helpMsg = GetMainMenuText();
        var keyboard = CreateMainMenuKeyboard();
        await TelegramNotifier.SendMessageAsync(token, chatId, helpMsg, keyboard);
    }

    private async Task HandleMenuNavigationAsync(string token, string chatId, long messageId, string menuKey)
    {
        string text;
        object keyboard;

        switch (menuKey)
        {
            case "menu_bag":
                text = "🎒 *Menu Pemeriksaan Tas & Gold*\nPilih slot tas yang ingin diperiksa:";
                keyboard = CreateBagMenuKeyboard();
                break;

            case "menu_tab":
                text = "🎯 *Menu Kontrol Auto Assist (Auto TAB)*\nPilih kontrol Auto TAB (per-slot atau serentak):";
                keyboard = CreateTabMenuKeyboard();
                break;

            case "menu_sml":
            case "menu_sml_1":
                text = "🏰 *Menu Clear SML — Level 1*\nPilih slot & kombinasi lantai SML Level 1:";
                keyboard = CreateSmlMenuKeyboard(1);
                break;

            case "menu_sml_2":
                text = "🏰 *Menu Clear SML — Level 2*\nPilih slot & kombinasi lantai SML Level 2:";
                keyboard = CreateSmlMenuKeyboard(2);
                break;

            case "menu_sml_3":
                text = "🏰 *Menu Clear SML — Level 3*\nPilih slot & kombinasi lantai SML Level 3:";
                keyboard = CreateSmlMenuKeyboard(3);
                break;

            case "menu_char":
                text = "🔄 *Menu Ganti Karakter*\nPilih nomor karakter atau klik Next untuk karakter 4-9:";
                keyboard = CreateCharMenuKeyboard();
                break;

            case "menu_char_s1":
                text = "🔄 *Ganti Karakter — Slot 1 (Karakter Berikutnya)*\nPilih nomor karakter untuk Slot 1:";
                keyboard = CreateCharNextMenuKeyboard(1);
                break;

            case "menu_char_s2":
                text = "🔄 *Ganti Karakter — Slot 2 (Karakter Berikutnya)*\nPilih nomor karakter untuk Slot 2:";
                keyboard = CreateCharNextMenuKeyboard(2);
                break;

            case "menu_char_s3":
                text = "🔄 *Ganti Karakter — Slot 3 (Karakter Berikutnya)*\nPilih nomor karakter untuk Slot 3:";
                keyboard = CreateCharNextMenuKeyboard(3);
                break;

            case "menu_main":
            default:
                text = GetMainMenuText();
                keyboard = CreateMainMenuKeyboard();
                break;
        }

        if (messageId > 0)
        {
            bool edited = await TelegramNotifier.EditMessageTextAsync(token, chatId, messageId, text, keyboard);
            if (!edited)
            {
                await TelegramNotifier.SendMessageAsync(token, chatId, text, keyboard);
            }
        }
        else
        {
            await TelegramNotifier.SendMessageAsync(token, chatId, text, keyboard);
        }
    }

    private static string GetMainMenuText()
    {
        return "🤖 *GameBotMonitor — Remote Control*\n\n" +
               "Silakan pilih kategori menu kontrol di bawah atau ketik perintah manual:\n\n" +
               "📊 `/status` - Cek status realtime ketiga slot\n" +
               "📸 `/ss` - Ambil live screenshot PC\n" +
               "▶️ `/start` / ⏹️ `/stop` - Nyalakan/matikan monitor\n" +
               "📐 `/tile` - Rapikan susunan 3 jendela game\n" +
               "🎒 `/allbag` / `/bag1`-`/bag3` - Cek tas\n" +
               "🎯 `/alltabon` / `/alltaboff` - Kontrol Auto TAB\n" +
               "🏰 `sml_1_1_max` - Clear SML (L1, L2, L3)\n" +
               "🔄 `/char [slot] [nomor]` - Ganti Karakter";
    }

    private static object CreateMainMenuKeyboard()
    {
        return new
        {
            inline_keyboard = new object[][]
            {
                // Baris 1: Status & Screenshot
                new object[]
                {
                    new { text = "📊 Status Realtime", callback_data = "/status" },
                    new { text = "📸 Live Screenshot", callback_data = "/ss" }
                },
                // Baris 2: Start / Stop Monitor
                new object[]
                {
                    new { text = "▶️ Mulai Monitor", callback_data = "/start" },
                    new { text = "⏹️ Berhenti Monitor", callback_data = "/stop" }
                },
                // Baris 3: Rapikan Jendela & Cek Tas Menu
                new object[]
                {
                    new { text = "📐 Rapikan Jendela", callback_data = "/tile" },
                    new { text = "🎒 Menu Cek Tas ❯", callback_data = "menu_bag" }
                },
                // Baris 4: Auto TAB & Clear SML Menu
                new object[]
                {
                    new { text = "🎯 Menu Auto TAB ❯", callback_data = "menu_tab" },
                    new { text = "🏰 Menu Clear SML ❯", callback_data = "menu_sml" }
                },
                // Baris 5: Ganti Karakter Menu
                new object[]
                {
                    new { text = "🔄 Menu Ganti Karakter ❯", callback_data = "menu_char" }
                }
            }
        };
    }

    private static object CreateBagMenuKeyboard()
    {
        return new
        {
            inline_keyboard = new object[][]
            {
                // Cek Semua Tas
                new object[]
                {
                    new { text = "🎒 Cek SEMUA Tas (Slot 1, 2, 3)", callback_data = "/allbag" }
                },
                // Cek Tas Per Slot
                new object[]
                {
                    new { text = "🎒 Tas Slot 1", callback_data = "/bag1" },
                    new { text = "🎒 Tas Slot 2", callback_data = "/bag2" },
                    new { text = "🎒 Tas Slot 3", callback_data = "/bag3" }
                },
                // Tombol Back
                new object[]
                {
                    new { text = "🔙 Kembali ke Menu Utama", callback_data = "menu_main" }
                }
            }
        };
    }

    private static object CreateTabMenuKeyboard()
    {
        return new
        {
            inline_keyboard = new object[][]
            {
                // Serentak All
                new object[]
                {
                    new { text = "🟢 TAB All ON", callback_data = "/alltabon" },
                    new { text = "⚪ TAB All OFF", callback_data = "/alltaboff" }
                },
                // Slot 1
                new object[]
                {
                    new { text = "🟢 TAB Slot 1 ON", callback_data = "/tab1on" },
                    new { text = "⚪ TAB Slot 1 OFF", callback_data = "/tab1off" }
                },
                // Slot 2
                new object[]
                {
                    new { text = "🟢 TAB Slot 2 ON", callback_data = "/tab2on" },
                    new { text = "⚪ TAB Slot 2 OFF", callback_data = "/tab2off" }
                },
                // Slot 3
                new object[]
                {
                    new { text = "🟢 TAB Slot 3 ON", callback_data = "/tab3on" },
                    new { text = "⚪ TAB Slot 3 OFF", callback_data = "/tab3off" }
                },
                // Tombol Back
                new object[]
                {
                    new { text = "🔙 Kembali ke Menu Utama", callback_data = "menu_main" }
                }
            }
        };
    }

    private static object CreateSmlMenuKeyboard(int level = 1)
    {
        if (level == 2)
        {
            return new
            {
                inline_keyboard = new object[][]
                {
                    // Baris 1: S1 L2 90 | S2 L2 90 | S3 L2 90
                    new object[]
                    {
                        new { text = "S1 L2 90", callback_data = "sml_1_2_90" },
                        new { text = "S2 L2 90", callback_data = "sml_2_2_90" },
                        new { text = "S3 L2 90", callback_data = "sml_3_2_90" }
                    },
                    // Baris 2: S1 L2 -2 | S2 L2 -2 | S3 L2 -2
                    new object[]
                    {
                        new { text = "S1 L2 -2", callback_data = "sml_1_2_-2" },
                        new { text = "S2 L2 -2", callback_data = "sml_2_2_-2" },
                        new { text = "S3 L2 -2", callback_data = "sml_3_2_-2" }
                    },
                    // Baris 3: S1 L2 -5 | S2 L2 -5 | S3 L2 -5
                    new object[]
                    {
                        new { text = "S1 L2 -5", callback_data = "sml_1_2_-5" },
                        new { text = "S2 L2 -5", callback_data = "sml_2_2_-5" },
                        new { text = "S3 L2 -5", callback_data = "sml_3_2_-5" }
                    },
                    // Baris 4: Prev Lvl & Next Lvl
                    new object[]
                    {
                        new { text = "❮ Prev Lvl", callback_data = "menu_sml_1" },
                        new { text = "Next Lvl ❯", callback_data = "menu_sml_3" }
                    },
                    // Baris 5: Back to Main
                    new object[]
                    {
                        new { text = "🔙 Kembali ke Menu Utama", callback_data = "menu_main" }
                    }
                }
            };
        }
        else if (level == 3)
        {
            return new
            {
                inline_keyboard = new object[][]
                {
                    // Baris 1: S1 L3 Max | S2 L3 Max | S3 L3 Max
                    new object[]
                    {
                        new { text = "S1 L3 Max", callback_data = "sml_1_3_max" },
                        new { text = "S2 L3 Max", callback_data = "sml_2_3_max" },
                        new { text = "S3 L3 Max", callback_data = "sml_3_3_max" }
                    },
                    // Baris 2: S1 L3 -2 | S2 L3 -2 | S3 L3 -2
                    new object[]
                    {
                        new { text = "S1 L3 -2", callback_data = "sml_1_3_-2" },
                        new { text = "S2 L3 -2", callback_data = "sml_2_3_-2" },
                        new { text = "S3 L3 -2", callback_data = "sml_3_3_-2" }
                    },
                    // Baris 3: S1 L3 -5 | S2 L3 -5 | S3 L3 -5
                    new object[]
                    {
                        new { text = "S1 L3 -5", callback_data = "sml_1_3_-5" },
                        new { text = "S2 L3 -5", callback_data = "sml_2_3_-5" },
                        new { text = "S3 L3 -5", callback_data = "sml_3_3_-5" }
                    },
                    // Baris 4: Prev Lvl & Back to Main
                    new object[]
                    {
                        new { text = "❮ Prev Lvl", callback_data = "menu_sml_2" },
                        new { text = "🔙 Kembali ke Menu Utama", callback_data = "menu_main" }
                    }
                }
            };
        }
        else // Level 1 (Default)
        {
            return new
            {
                inline_keyboard = new object[][]
                {
                    // Baris 1: S1 L1 Max | S2 L1 Max | S3 L1 Max
                    new object[]
                    {
                        new { text = "S1 L1 Max", callback_data = "sml_1_1_max" },
                        new { text = "S2 L1 Max", callback_data = "sml_2_1_max" },
                        new { text = "S3 L1 Max", callback_data = "sml_3_1_max" }
                    },
                    // Baris 2: S1 L1 -2 | S2 L1 -2 | S3 L1 -2
                    new object[]
                    {
                        new { text = "S1 L1 -2", callback_data = "sml_1_1_-2" },
                        new { text = "S2 L1 -2", callback_data = "sml_2_1_-2" },
                        new { text = "S3 L1 -2", callback_data = "sml_3_1_-2" }
                    },
                    // Baris 3: S1 L1 -5 | S2 L1 -5 | S3 L1 -5
                    new object[]
                    {
                        new { text = "S1 L1 -5", callback_data = "sml_1_1_-5" },
                        new { text = "S2 L1 -5", callback_data = "sml_2_1_-5" },
                        new { text = "S3 L1 -5", callback_data = "sml_3_1_-5" }
                    },
                    // Baris 4: Back to Main & Next Lvl
                    new object[]
                    {
                        new { text = "🔙 Menu Utama", callback_data = "menu_main" },
                        new { text = "Next Lvl ❯", callback_data = "menu_sml_2" }
                    }
                }
            };
        }
    }

    private static object CreateCharMenuKeyboard()
    {
        return new
        {
            inline_keyboard = new object[][]
            {
                // Slot 1: S1 Char 1 | S1 Char 2 | S1 Char 3 | S1 Next
                new object[]
                {
                    new { text = "S1 Char 1", callback_data = "/char1 1" },
                    new { text = "S1 Char 2", callback_data = "/char1 2" },
                    new { text = "S1 Char 3", callback_data = "/char1 3" },
                    new { text = "S1 Next ❯", callback_data = "menu_char_s1" }
                },
                // Slot 2: S2 Char 1 | S2 Char 2 | S2 Char 3 | S2 Next
                new object[]
                {
                    new { text = "S2 Char 1", callback_data = "/char2 1" },
                    new { text = "S2 Char 2", callback_data = "/char2 2" },
                    new { text = "S2 Char 3", callback_data = "/char2 3" },
                    new { text = "S2 Next ❯", callback_data = "menu_char_s2" }
                },
                // Slot 3: S3 Char 1 | S3 Char 2 | S3 Char 3 | S3 Next
                new object[]
                {
                    new { text = "S3 Char 1", callback_data = "/char3 1" },
                    new { text = "S3 Char 2", callback_data = "/char3 2" },
                    new { text = "S3 Char 3", callback_data = "/char3 3" },
                    new { text = "S3 Next ❯", callback_data = "menu_char_s3" }
                },
                // Tombol Back
                new object[]
                {
                    new { text = "🔙 Kembali ke Menu Utama", callback_data = "menu_main" }
                }
            }
        };
    }

    private static object CreateCharNextMenuKeyboard(int slot)
    {
        return new
        {
            inline_keyboard = new object[][]
            {
                // Baris 1: Char 4, 5, 6
                new object[]
                {
                    new { text = $"S{slot} Char 4", callback_data = $"/char{slot} 4" },
                    new { text = $"S{slot} Char 5", callback_data = $"/char{slot} 5" },
                    new { text = $"S{slot} Char 6", callback_data = $"/char{slot} 6" }
                },
                // Baris 2: Char 7, 8, 9
                new object[]
                {
                    new { text = $"S{slot} Char 7", callback_data = $"/char{slot} 7" },
                    new { text = $"S{slot} Char 8", callback_data = $"/char{slot} 8" },
                    new { text = $"S{slot} Char 9", callback_data = $"/char{slot} 9" }
                },
                // Navigasi: Back to Char Menu & Back to Main
                new object[]
                {
                    new { text = "❮ Menu Karakter", callback_data = "menu_char" },
                    new { text = "🔙 Menu Utama", callback_data = "menu_main" }
                }
            }
        };
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

    private async Task HandleEnterSmlAsync(string token, string chatId, int slotNum, int smlLevel, string floorTarget)
    {
        await TelegramNotifier.SendMessageAsync(
            token,
            chatId,
            $"🏰 Memulai proses *Clear SML* untuk *Slot {slotNum}*...\n" +
            $"⭐ Level: {smlLevel} | 🚪 Target: {floorTarget}\n" +
            $"Mohon tunggu proses navigasi & loading in-dungeon...");

        if (OnEnterSml != null)
        {
            var (success, imagePath, message) = await OnEnterSml.Invoke(slotNum, smlLevel, floorTarget);
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
            await TelegramNotifier.SendMessageAsync(token, chatId, "⚠️ Handler Clear SML belum siap.");
        }
    }
}
