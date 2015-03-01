﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Timers;
using log4net;
using log4net.Core;
using Newtonsoft.Json.Linq;
using QueryHelper;
using SqlAlertService.Models;
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
        private Dictionary<string, DateTime> lastRun;
        private SqlAlertServiceConfig currentServiceConfig;

        public ActionManager()
        {
            lastRun = new Dictionary<string, DateTime>();
            Running = false;
            timer = new Timer(1000*5);
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
                        LoadServiceConfigFromFile();
                        foreach (var alertConfig in currentServiceConfig.Alerts)
                        {
                            if (AlertDue(alertConfig))
                            {
                                ExecuteAlert(alertConfig);
                                UpdateLastRun(alertConfig.Name);
                            }
                        }
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

        private void ExecuteAlert(AlertConfig alertConfig)
        {
            var result = ExecuteSql(alertConfig);
            if (debug)
            {
                log.Debug("Executing SQL for alert " + alertConfig.Name + " which returned the value: '" + result + "'");
            }
            if (string.Compare(result, alertConfig.AlertValue, StringComparison.InvariantCultureIgnoreCase) == 0)
            {
                SendAlert(alertConfig, result);
            }
            else if (string.Compare(result, alertConfig.NotifyValue, StringComparison.InvariantCultureIgnoreCase) == 0)
            {
                SendNotify(alertConfig, result);
            }
        }

        private void SendAlert(AlertConfig alertConfig, string value)
        {
            var subject = "SqlAlertService ALERT!" + alertConfig.Name;
            var body = string.Format("This email was generated by alert {0}. \r\n The following value was returned from the database: '{2}'. \r\n {1} \r\n SQL: {5} \r\n This email was generated at {3} and sent from {4}. ", 
                alertConfig.Name, alertConfig.Description, value, DateTime.Now, Environment.MachineName, alertConfig.SqlStatement);
            log.Debug("Sending email with body: \r\n" + body);
            SendMailgunEmail(subject, body, true);
        }

        private void SendNotify(AlertConfig alertConfig, string value)
        {
            var subject = "SqlAlertService Notify. " + alertConfig.Name;
            var body = string.Format("RED ALERT!! \r\n This email was generated by alert {0}. \r\n The following value was returned from the database: '{2}'. \r\n {1} \r\n SQL: {5} \r\n This email was generated at {3} and sent from {4}. ",
                alertConfig.Name, alertConfig.Description, value, DateTime.Now, Environment.MachineName, alertConfig.SqlStatement);
            log.Debug("Sending email with body: \r\n" + body);
            SendMailgunEmail(subject, body);
        }

        private string ExecuteSql(AlertConfig alertConfig)
        {
            try
            {
                var connStringConfig = currentServiceConfig.ConnectionStrings.FirstOrDefault(c => c.Name == alertConfig.ConnectionStringName);
                var runner = new QueryRunner(connStringConfig.ConnectionString, connStringConfig.Provider);
                return runner.RunScalerQuery<string>(alertConfig.SqlStatement);
            }
            catch (Exception ex)
            {
                log.Error(ex);
                return "null";
            }
        }

        private void UpdateLastRun(string alertName)
        {
            lastRun[alertName] = DateTime.Now;
        }

        private bool AlertDue(AlertConfig alertConfig)
        {
            if (!lastRun.ContainsKey(alertConfig.Name)) return true;
            return DateTime.Now.Subtract(lastRun[alertConfig.Name]) > alertConfig.Frequency;
        }

        private void LoadServiceConfigFromFile()
        {
            var executingAssembly = Assembly.GetExecutingAssembly();
            var executingPath = executingAssembly.Location.Replace(executingAssembly.GetName().Name + ".exe", "");
            var configFilePath = ConfigurationManager.AppSettings["alertServiceConfigFile"];
            var fullPath = Path.Combine(executingPath, configFilePath);
            string filebody;
            using (var reader = new StreamReader(fullPath))
            {
                filebody = reader.ReadToEnd();
            }
            var jobject = JObject.Parse(filebody);
            currentServiceConfig = jobject.ToObject<SqlAlertServiceConfig>();
        }

        private void SendMailgunEmail(string subject, string body, bool redAlert = false)
        {
            var sendTo = currentServiceConfig.EmailConfig.ToEmailAddressNotify.Split(',');
            var sendToRedAlert = currentServiceConfig.EmailConfig.ToEmailAddressAlert.Split(',');

            var formContentData = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("from", currentServiceConfig.EmailConfig.FromEmailAddress),
                new KeyValuePair<string, string>("subject", subject),
                new KeyValuePair<string, string>("text", body)
            };

            if (redAlert)
            {
                formContentData.AddRange(sendToRedAlert.Select(s => new KeyValuePair<string, string>("to", s)).ToList());
            }
            else
            {
                formContentData.AddRange(sendTo.Select(s => new KeyValuePair<string, string>("to", s)).ToList());
            }

            using (var httpclient = new HttpClient())
            {
                var byteArray = Encoding.ASCII.GetBytes("api:" + currentServiceConfig.EmailConfig.MailGunApiKey);
                httpclient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
                var request = new HttpRequestMessage(HttpMethod.Post, currentServiceConfig.EmailConfig.MailGunApiUrl)
                {
                    Content = new FormUrlEncodedContent(formContentData.ToArray())
                };
                var response = httpclient.SendAsync(request).Result;
                if (!response.IsSuccessStatusCode)
                {
                    throw new ApplicationException("The call to mailgun returned " + response.StatusCode);
                }
            }
        }
    }
}
