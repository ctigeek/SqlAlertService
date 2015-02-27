using System;
using System.Threading;
using System.Timers;
using log4net;
using log4net.Core;
using Timer = System.Timers.Timer;

namespace SqlAlertService
{
    internal class ActionManager
    {
        private static ILog log = LogManager.GetLogger(typeof(ActionManager));
        private static object lockObject = new object();
        private Timer timer;
        public bool Running { get; private set; }
        private readonly bool debug;

        public ActionManager()
        {
            Running = false;
            timer = new Timer(1000*15); //15 seconds.... you'll probably want to change this....
            timer.Elapsed += timer_Elapsed;

            debug = ((log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository()).Root.Level.Value <= Level.Debug.Value;
        }

        public void Start()
        {
            if (Running) throw new InvalidOperationException("ActionManager already running.");
            timer.Start();
            Running = true;
        }

        public void Stop()
        {
            lock (lockObject)
            {
                Running = false;
                if (timer.Enabled) timer.Stop();
            }
        }

        private void timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (Monitor.TryEnter(lockObject))
            {
                try
                {
                    if (Running)
                    {
                        //do stuff here....
                    }
                }
                catch (Exception ex)
                {
                    log.Error(ex.ToString());
                }
                finally
                {
                    Monitor.Exit(lockObject);
                }
            }
        }
    }
}
