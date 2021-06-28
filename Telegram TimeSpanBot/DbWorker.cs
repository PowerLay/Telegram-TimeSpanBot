using System;
using System.Collections.Generic;
using System.Device.Location;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Telegram_TimeSpanBot.Configure;
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
            { ChatId = chatId, StartTime = timeStart, MessageId = messageId });

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

        public static async Task AddTimeSpan(long chatId, DateTime start, DateTime end)
        {
            await using var context = new TimeSpanDBContext();

            await context.TimeSpans.AddAsync(new TimeSpanUnit { ChatId = chatId, StartTime = start, StopTime = end });

            await context.SaveChangesAsync();
        }

        public static async Task<bool> CheckCoord(int messageId, GeoCoordinate location)
        {
            await using var context = new TimeSpanDBContext();

            var locationByMessageId = await context.Locations.FirstOrDefaultAsync(x => x.MessageId == messageId);
            if (locationByMessageId == null)
            {
                await context.Locations.AddAsync(new location
                {
                    MessageId = messageId,
                    Latitude = location.Latitude,
                    Longitude = location.Longitude,
                    StartTrack = DateTime.Now
                });

                await context.SaveChangesAsync();
                return true;
            }

            if (locationByMessageId.StopTrack != new DateTime())
                return true;

            var distanceToStartCord = location.GetDistanceTo(new GeoCoordinate(
                locationByMessageId.Latitude,
                locationByMessageId.Longitude));

            if (!(Settings.MaxDistance < distanceToStartCord)) return true;

            locationByMessageId.StopTrack = DateTime.Now;
            await context.SaveChangesAsync();
            return false;
        }

        public static async Task<TimeSpanUnit> GetLocationTimeSpanUnit(int messageId)
        {
            await using var context = new TimeSpanDBContext();
            var location = await context.Locations.FirstOrDefaultAsync(x => x.MessageId == messageId);
            await context.SaveChangesAsync();
            var res = new TimeSpanUnit()
            {
                MessageId = messageId,
                StartTime = location.StartTrack,
                StopTime = location.StopTrack
            };
            return res;
        }

        public static async Task<List<TimeSpanUnit>> GetListTimeSpanUnits(long chatId)
        {
            await using var context = new TimeSpanDBContext();

            var res = context.TimeSpans.Where(x => x.ChatId == chatId).ToList();

            await context.SaveChangesAsync();

            return res;
        }

        public static async Task<bool> RemoveTimeSpanUnit(long id)
        {
            await using var context = new TimeSpanDBContext();

            var timeSpanUnit = context.TimeSpans.FirstOrDefault(x => x.Id == id);
            if (timeSpanUnit == null)
                return false;

            context.TimeSpans.Remove(timeSpanUnit);

            await context.SaveChangesAsync();
            return true;
        }
    }
}