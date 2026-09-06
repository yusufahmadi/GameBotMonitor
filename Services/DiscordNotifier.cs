using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace GameBotMonitor.Services;

public static class DiscordNotifier
{
    private static readonly HttpClient HttpClient = new();

    public static async Task<(bool Success, string Message)> SendAlertAsync(
        string webhookUrl, 
        int slotIndex, 
        string windowTitle, 
        string imagePath, 
        bool mentionEveryone = true,
        string gameName = "Grand Fantasia")
    {
        if (string.IsNullOrWhiteSpace(webhookUrl))
            return (false, "URL Webhook kosong / Webhook URL is empty.");

        try
        {
            bool hasImage = !string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath);
            string appGameName = string.IsNullOrWhiteSpace(gameName) ? "Game" : gameName;

            string alertTitle = LanguageService.Format("DiscordAlertTitle", slotIndex);
            string alertDesc = LanguageService.Format("DiscordAlertDesc", slotIndex, windowTitle);
            string mentionMsg = mentionEveryone ? LanguageService.Get("DiscordMentionMsg") : "";
            string fieldSlot = LanguageService.Get("DiscordFieldSlot");
            string fieldWindow = LanguageService.Get("DiscordFieldWindow");
            string fieldTime = LanguageService.Get("DiscordFieldTime");
            string fieldStatus = LanguageService.Get("DiscordFieldStatus");
            string statusDead = LanguageService.Get("DiscordStatusDead");

            // 1. Buat struktur embed
            var embed = new
            {
                title = alertTitle,
                description = alertDesc,
                color = 0xE74C3C, // Merah
                fields = new[]
                {
                    new { name = fieldSlot, value = $"Slot {slotIndex}", inline = true },
                    new { name = fieldWindow, value = string.IsNullOrWhiteSpace(windowTitle) ? appGameName : windowTitle, inline = true },
                    new { name = fieldTime, value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), inline = true },
                    new { name = fieldStatus, value = statusDead, inline = false }
                },
                image = hasImage ? new { url = "attachment://screenshot.png" } : null,
                footer = new { text = $"{appGameName} Bot Health Monitor" },
                timestamp = DateTime.UtcNow.ToString("o")
            };

            // 2. Coba kirim dengan multipart (lampiran screenshot) jika ada gambar
            if (hasImage)
            {
                try
                {
                    using var multipart = new MultipartFormDataContent();

                    var payloadWithAttachment = new
                    {
                        content = mentionMsg,
                        embeds = new[] { embed },
                        attachments = new[]
                        {
                            new { id = 0, filename = "screenshot.png", description = "Screenshot Game" }
                        }
                    };

                    var jsonPayload = JsonSerializer.Serialize(payloadWithAttachment);
                    multipart.Add(new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json"), "payload_json");

                    var imageBytes = await File.ReadAllBytesAsync(imagePath);
                    var filePart = new ByteArrayContent(imageBytes);
                    filePart.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                    multipart.Add(filePart, "files[0]", "screenshot.png");

                    var multiResponse = await HttpClient.PostAsync(webhookUrl, multipart);
                    if (multiResponse.IsSuccessStatusCode)
                    {
                        return (true, "Berhasil terkirim dengan screenshot / Sent with screenshot.");
                    }

                    // Jika multipart gagal (misal file size limit / format), baca error dan coba fallback JSON sederhana
                    var multiError = await multiResponse.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[DiscordNotifier] Multipart failed: {multiError}. Falling back to simple embed.");
                }
                catch (Exception multiEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[DiscordNotifier] Multipart exception: {multiEx.Message}. Falling back to simple embed.");
                }
            }

            // 3. Fallback: Kirim embed JSON sederhana (pasti berhasil sama seperti tombol test)
            var simpleEmbed = new
            {
                title = alertTitle,
                description = alertDesc,
                color = 0xE74C3C,
                fields = new[]
                {
                    new { name = fieldSlot, value = $"Slot {slotIndex}", inline = true },
                    new { name = fieldWindow, value = string.IsNullOrWhiteSpace(windowTitle) ? appGameName : windowTitle, inline = true },
                    new { name = fieldTime, value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), inline = true },
                    new { name = fieldStatus, value = statusDead, inline = false }
                },
                footer = new { text = $"{appGameName} Bot Health Monitor" },
                timestamp = DateTime.UtcNow.ToString("o")
            };

            var simplePayload = new
            {
                content = mentionMsg,
                embeds = new[] { simpleEmbed }
            };

            var simpleJson = JsonSerializer.Serialize(simplePayload);
            using var simpleContent = new StringContent(simpleJson, System.Text.Encoding.UTF8, "application/json");
            var response = await HttpClient.PostAsync(webhookUrl, simpleContent);

            if (response.IsSuccessStatusCode)
            {
                return (true, "Berhasil terkirim (embed teks) / Sent successfully (text embed).");
            }

            var errorBody = await response.Content.ReadAsStringAsync();
            return (false, $"HTTP {(int)response.StatusCode}: {errorBody}");
        }
        catch (Exception ex)
        {
            return (false, $"Exception: {ex.Message}");
        }
    }

    public static async Task<bool> SendTestAsync(string webhookUrl)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl)) return false;

        try
        {
            var payload = new
            {
                content = LanguageService.Get("DiscordTestContent"),
                embeds = new[]
                {
                    new
                    {
                        title = LanguageService.Get("DiscordTestTitle"),
                        description = LanguageService.Get("DiscordTestDesc"),
                        color = 0x2ECC71, // Hijau
                        timestamp = DateTime.UtcNow.ToString("o")
                    }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await HttpClient.PostAsync(webhookUrl, content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DiscordNotifier] Test error: {ex.Message}");
            return false;
        }
    }
}
