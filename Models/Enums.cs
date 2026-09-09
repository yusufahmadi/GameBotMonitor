namespace GameBotMonitor.Models;

public enum ClientStatus
{
    Offline,        // Jendela game tidak ditemukan
    Alive,          // Karakter hidup normal (HP > 0)
    LowHp,          // HP kritis
    Dead,           // Karakter mati (Darah 0)
    Crashed,        // Proses game tertutup / crash
    NotResponding,  // Jendela game freeze / tidak merespons
    Switching       // Sedang dalam proses pergantian karakter
}
