using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Telegram_TimeSpanBot.TimeSpansDB;

namespace Telegram_TimeSpanBot
{
    internal static class DbWorker
    {
        public static async Task<TimeSpan> GetTimeSpanAtInterval(DateTime start, DateTime end)
        {
            var res = new TimeSpan();

            await using var context = new TimeSpanDBContext();
            var tmp = context.TimeSpans.Where(x => x.StartTime > start && x.StopTime < end);
            foreach (var timeSpanUnit in tmp)
                if (timeSpanUnit.StopTime != new DateTime())
                    res += timeSpanUnit.StopTime - timeSpanUnit.StartTime;
            await context.SaveChangesAsync();

            return res;
        }

        public static async Task SaveTimeStart(long chatId, int messageId, DateTime timeStart)
        {
            await using var context = new TimeSpanDBContext();
            await context.TimeSpans.AddAsync(new TimeSpanUnit
                {ChatId = chatId, StartTime = timeStart, MessageId = messageId});
            await context.SaveChangesAsync();
        }

        public static async Task SaveTimeStop(long chatId, int messageId, DateTime timeStop)
        {
            await using var context = new TimeSpanDBContext();
            var tmp = await GetTimeSpanUnit(chatId, messageId);
            tmp.StopTime = timeStop;
            context.Entry(tmp).State = EntityState.Modified;
            await context.SaveChangesAsync();
        }

        private static async Task<TimeSpanUnit> GetTimeSpanUnit(long chatId, int messageId)
        {
            await using var context = new TimeSpanDBContext();
            var timeSpanByMessageId = await context.TimeSpans.FirstOrDefaultAsync(x => x.MessageId == messageId);
            var timeSpanByChatId = context.TimeSpans.OrderBy(o => o.StartTime).Last(x => x.ChatId == chatId);

            var res = timeSpanByMessageId ?? timeSpanByChatId;

            return res;
        }

        public static async Task<TimeSpan> GetTimeSpan(long chatId, int messageId)
        {
            await using var context = new TimeSpanDBContext();
            var tmp = await GetTimeSpanUnit(chatId, messageId);
            var res = tmp.StopTime - tmp.StartTime;

            return res;
        }
    }
}