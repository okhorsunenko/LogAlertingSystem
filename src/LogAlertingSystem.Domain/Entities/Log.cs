using LogAlertingSystem.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogAlertingSystem.Domain.Entities
{
    // unified log which where could be combined logs from widnows/linux/macos
    public class Log
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public int? EventId { get; set; }
        public EventLogLevel Level { get; set; }
        public string Source { get; set; }
        public string Type { get; set; }
        public string Message { get; set; }
    }
}