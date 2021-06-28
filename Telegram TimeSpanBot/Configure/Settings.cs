using System.IO;
using Newtonsoft.Json;

namespace Telegram_TimeSpanBot.Configure
{
    internal static class Settings
    {
        public static string Token { get; set; } = File.ReadAllText("token.data");
        public static double MaxDistance { get; set; } = 50;
    }
}