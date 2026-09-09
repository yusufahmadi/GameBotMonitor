using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace GameBotMonitor.Services;

public static class TelegramNotifier
{
    private static readonly HttpClient HttpClient = new();

    public static async Task<bool> SendAlertAsync(string botToken, string chatId, int slotIndex, string windowTitle, string imagePath, string gameName = "Grand Fantasia")
    {
        if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(chatId)) return false;

        string appGameName = string.IsNullOrWhiteSpace(gameName) ? "Game" : gameName;
        string windowName = string.IsNullOrWhiteSpace(windowTitle) ? appGameName : windowTitle;
        string timeStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var caption = LanguageService.Format("TelegramAlertCaption", slotIndex, windowName, timeStr);

        try
        {
            if (File.Exists(imagePath))
            {
                var url = $"https://api.telegram.org/bot{botToken}/sendPhoto";
                using var content = new MultipartFormDataContent();
                content.Add(new StringContent(chatId), "chat_id");
                content.Add(new StringContent(caption), "caption");
                content.Add(new StringContent("Markdown"), "parse_mode");

                var imageBytes = await File.ReadAllBytesAsync(imagePath);
                var fileContent = new ByteArrayContent(imageBytes);
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
                content.Add(fileContent, "photo", "screenshot.png");

                var response = await HttpClient.PostAsync(url, content);
                if (response.IsSuccessStatusCode) return true;
            }

            // Fallback ke sendMessage teks jika sendPhoto gagal
            return await SendMessageAsync(botToken, chatId, caption);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TelegramNotifier] SendAlert error: {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> SendTestAsync(string botToken, string chatId)
    {
        var text = LanguageService.Get("TelegramTestMsg");
        return await SendMessageAsync(botToken, chatId, text);
    }

    public static async Task<bool> SendMessageAsync(string botToken, string chatId, string text, object? replyMarkup = null)
    {
        try
        {
            var url = $"https://api.telegram.org/bot{botToken}/sendMessage";
            object payload = replyMarkup != null
                ? new
                {
                    chat_id = chatId,
                    text = text,
                    parse_mode = "Markdown",
                    reply_markup = replyMarkup
                }
                : new
                {
                    chat_id = chatId,
                    text = text,
                    parse_mode = "Markdown"
                };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await HttpClient.PostAsync(url, content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TelegramNotifier] SendMessage error: {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> AnswerCallbackQueryAsync(string botToken, string callbackQueryId, string? text = null)
    {
        try
        {
            var url = $"https://api.telegram.org/bot{botToken}/answerCallbackQuery";
            var payload = new
            {
                callback_query_id = callbackQueryId,
                text = text
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await HttpClient.PostAsync(url, content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TelegramNotifier] AnswerCallbackQuery error: {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> EditMessageTextAsync(string botToken, string chatId, long messageId, string text, object? replyMarkup = null)
    {
        try
        {
            var url = $"https://api.telegram.org/bot{botToken}/editMessageText";
            object payload = replyMarkup != null
                ? new
                {
                    chat_id = chatId,
                    message_id = messageId,
                    text = text,
                    parse_mode = "Markdown",
                    reply_markup = replyMarkup
                }
                : new
                {
                    chat_id = chatId,
                    message_id = messageId,
                    text = text,
                    parse_mode = "Markdown"
                };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await HttpClient.PostAsync(url, content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TelegramNotifier] EditMessageText error: {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> SendPhotoDirectAsync(string botToken, string chatId, string imagePath, string caption)
    {
        if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(chatId)) return false;

        try
        {
            if (!File.Exists(imagePath))
            {
                return await SendMessageAsync(botToken, chatId, caption);
            }

            var url = $"https://api.telegram.org/bot{botToken}/sendPhoto";
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(chatId), "chat_id");
            if (!string.IsNullOrWhiteSpace(caption))
            {
                content.Add(new StringContent(caption), "caption");
                content.Add(new StringContent("Markdown"), "parse_mode");
            }

            var imageBytes = await File.ReadAllBytesAsync(imagePath);
            var fileContent = new ByteArrayContent(imageBytes);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
            content.Add(fileContent, "photo", "image.png");

            var response = await HttpClient.PostAsync(url, content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TelegramNotifier] SendPhotoDirect error: {ex.Message}");
            return false;
        }
    }
}
