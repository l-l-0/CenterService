//------------------------------------------------------------------------------
// @license
// Copyright © 北京盛福瑞安科技有限责任公司. All Rights Reserved.
// 
// Use of this source code is governed by an BSD-style.
//------------------------------------------------------------------------------

using Exceptionless;
using HealthCare.WebService.Jobs;
using log4net;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Web;

#pragma warning disable CS1591

namespace HealthCare.WebService
{
    public class Global : HttpApplication
    {
        internal static ILog GlobalLogger = LogManager.GetLogger("global");
        internal static ILog MonitorLogger = LogManager.GetLogger("monitor");
        internal static ILog SchedulerLogger = LogManager.GetLogger("scheduler");
        internal static JObject AppSettings = JObject.Parse(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "AppSettings.json")));
        internal static string Hospital = MongoData.Helper.GetHospital();

        protected void Application_Start(object sender, EventArgs e)
        {
            ExceptionlessClient.Default.Startup();
            switch (Hospital)
            {
                case MongoData.Hospital.SZGJ: SZGJPatientSyncScheduler.Start(); break;
                case MongoData.Hospital.BJFC: BJFCPatientSyncScheduler.Start(); break;
                case MongoData.Hospital.SDBZ: SDBZPrescriptionsSyncScheduler.Start(); break;
                case MongoData.Hospital.SDSL: SDSLOperationSyncScheduler.Start(); break;
                case MongoData.Hospital.BJTT: BJTTOperationSyncScheduler.Start(); break;
            }
            GlobalLogger.Info($"{nameof(Application_Start)} => '{Hospital}'");
        }

        protected void Session_Start(object sender, EventArgs e)
        {

        }

        protected void Application_BeginRequest(object sender, EventArgs e)
        {
            if (Context.Request.Url.LocalPath == "/")
            {
                string redirect;
                switch (Hospital)
                {
                    case MongoData.Hospital.SZGJ: redirect = "/WebService/GuangJiWebService.asmx"; break;
                    case MongoData.Hospital.SDSL_BY: redirect = "/WebService/SDSLBYWebService.asmx"; break;
                    default: redirect = $"/WebService/{Hospital}WebService.asmx"; break;
                }
                HttpContext.Current.Response.Redirect(redirect);
            }

            MonitorLogger.Info($"{Context.Request.UserHostAddress}\t{Context.Request.Url.PathAndQuery}");
        }

        protected void Application_AuthenticateRequest(object sender, EventArgs e)
        {

        }

        protected void Application_Error(object sender, EventArgs e)
        {
            GlobalLogger.Error(nameof(Application_Error), Context.Error);
        }

        protected void Application_End(object sender, EventArgs e)
        {
            GlobalLogger.Info(nameof(Application_End));
        }
    }
}