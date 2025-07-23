using System;
using System.IO;
using System.Media;

namespace FlexGuard.Core.Util
{
    public static class NotificationHelper
    {
        public static void PlayBackupCompleteSound()
        {
            string filePath = Path.Combine(AppContext.BaseDirectory, "FlexGuard_Backup_Complete_Retro.wav");

            if (OperatingSystem.IsWindows())
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        using var player = new SoundPlayer(filePath);
                        player.PlaySync(); // Afspiller og venter til den er færdig
                    }
                    else
                    {
                        Console.Beep(1000, 500);
                        Console.Beep(1200, 300);
                    }
                }
                catch
                {
                    Console.Beep(1000, 500);
                    Console.Beep(1200, 300);
                }
            }
        }
    }
}
