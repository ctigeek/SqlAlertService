namespace SqlAlertService.Models
{
    class SqlAlertServiceConfig
    {
        public ConnectionStringConfig[] ConnectionStrings { get; set; }
        public EmailConfig EmailConfig { get; set; }
        public AlertConfig[] Alerts { get; set; }
    }
}
