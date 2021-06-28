using System;
using System.Collections.Generic;
using System.Linq;
using System.Device.Location;
using System.Text;
using System.Threading.Tasks;

namespace Telegram_TimeSpanBot.TimeSpansDB
{
    public class location
    {
        public int Id { get; set; }
        public int MessageId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime StartTrack { get; set; }
        public DateTime StopTrack { get; set; }
    }
}
