using Microsoft.EntityFrameworkCore;

namespace Telegram_TimeSpanBot.TimeSpansDB
{
    public class TimeSpanDBContext : DbContext
    {
        public DbSet<TimeSpanUnit> TimeSpans { get; set; }
        public DbSet<location> Locations { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(@"Server=(localdb)\mssqllocaldb;Database=TimeSpanDB;Trusted_Connection=True;");
        }
    }
}