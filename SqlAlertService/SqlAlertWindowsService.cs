using System.ServiceProcess;

namespace SqlAlertService
{
    public partial class SqlAlertWindowsService : ServiceBase
    {
        private readonly ActionManager actionManager;
        public SqlAlertWindowsService()
        {
            InitializeComponent();
            actionManager = new ActionManager();
        }

        protected override void OnStart(string[] args)
        {
            actionManager.Start();
        }

        protected override void OnStop()
        {
            actionManager.Stop();
        }
    }
}
