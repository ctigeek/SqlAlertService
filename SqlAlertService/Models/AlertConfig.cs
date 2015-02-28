using System;

namespace SqlAlertService.Models
{
    class AlertConfig
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public TimeSpan Frequency { get; set; }
        public string SqlStatement { get; set; }
        public string ConnectionStringName { get; set; }
        public string NotifyValue { get; set; }
        public string AlertValue { get; set; }
    }
}