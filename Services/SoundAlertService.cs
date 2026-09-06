using System.IO;
using System.Media;

namespace GameBotMonitor.Services;

public class SoundAlertService
{
    private SoundPlayer? _player;
    private bool _isPlaying;
    private readonly object _lock = new();

    public bool IsPlaying
    {
        get { lock (_lock) return _isPlaying; }
        private set { lock (_lock) _isPlaying = value; }
    }

    /// <summary>
    /// Memutar alarm secara terus-menerus (looping) sampai Stop() dipanggil
    /// </summary>
    public void PlayLooping(string? customAudioPath = null)
    {
        lock (_lock)
        {
            Stop();
            try
            {
                _player = CreatePlayer(customAudioPath);
                _player.PlayLooping();
                IsPlaying = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SoundAlertService] PlayLooping error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Menghentikan suara alarm
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            if (_player != null)
            {
                try
                {
                    _player.Stop();
                    _player.Dispose();
                }
                catch { }
                _player = null;
            }
            IsPlaying = false;
        }
    }

    /// <summary>
    /// Memutar suara 1 kali untuk tes audio
    /// </summary>
    public void TestSound(string? customAudioPath = null)
    {
        Task.Run(() =>
        {
            try
            {
                using var player = CreatePlayer(customAudioPath);
                player.PlaySync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SoundAlertService] TestSound error: {ex.Message}");
            }
        });
    }

    private static SoundPlayer CreatePlayer(string? customAudioPath)
    {
        if (!string.IsNullOrWhiteSpace(customAudioPath) && File.Exists(customAudioPath))
        {
            return new SoundPlayer(customAudioPath);
        }

        // Jika tidak ada file kustom, buat suara sirine sintetis WAV di memory
        var stream = GenerateSirenWavStream();
        return new SoundPlayer(stream);
    }

    /// <summary>
    /// Menghasilkan file audio WAV sirine dua nada (800Hz & 1200Hz) secara terprogram di memori
    /// </summary>
    private static MemoryStream GenerateSirenWavStream()
    {
        const int sampleRate = 44100;
        const short bitsPerSample = 16;
        const short channels = 1;
        const double durationSeconds = 1.2;
        var totalSamples = (int)(sampleRate * durationSeconds);

        var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            // RIFF header
            writer.Write("RIFF"u8);
            writer.Write(36 + totalSamples * 2);
            writer.Write("WAVE"u8);

            // fmt subchunk
            writer.Write("fmt "u8);
            writer.Write(16); // subchunk size
            writer.Write((short)1); // PCM
            writer.Write(channels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * channels * (bitsPerSample / 8)); // byte rate
            writer.Write((short)(channels * (bitsPerSample / 8)));     // block align
            writer.Write(bitsPerSample);

            // data subchunk
            writer.Write("data"u8);
            writer.Write(totalSamples * 2);

            // Generate Siren Wave (Alternating 800Hz / 1200Hz)
            for (int i = 0; i < totalSamples; i++)
            {
                double time = (double)i / sampleRate;
                // Sirine berubah nada setiap 0.3 detik
                double freq = ((int)(time / 0.3) % 2 == 0) ? 880.0 : 1320.0;
                double angle = 2.0 * Math.PI * freq * time;
                short sample = (short)(Math.Sin(angle) * 26000);
                writer.Write(sample);
            }
        }

        ms.Position = 0;
        return ms;
    }
}
