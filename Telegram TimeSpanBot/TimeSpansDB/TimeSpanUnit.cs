using System;

namespace Telegram_TimeSpanBot.TimeSpansDB
{
    public class TimeSpanUnit
    {
        public long Id { get; set; }
        public long ChatId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime StopTime { get; set; }
        public int MessageId { get; set; }
    }
}