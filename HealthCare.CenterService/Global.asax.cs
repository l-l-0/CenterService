//------------------------------------------------------------------------------
// @license
// Copyright © 北京盛福瑞安科技有限责任公司. All Rights Reserved.
// 
// Use of this source code is governed by an BSD-style.
//------------------------------------------------------------------------------

using Exceptionless;
using HealthCare.CenterService.Jobs;
using log4net;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

#pragma warning disable CS1591

namespace HealthCare.CenterService
{
    public class Global : HttpApplication
    {
        internal static ILog GlobalLogger = LogManager.GetLogger("global");
        internal static ILog MonitorLogger = LogManager.GetLogger("monitor");
        internal static ILog SchedulerLogger = LogManager.GetLogger("scheduler");
        internal static JObject AppSettings => JObject.Parse(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "AppSettings.json")));
        internal static string Hospital = MongoData.Helper.GetHospital();

        protected void Application_Start(object sender, EventArgs e)
        {
            GlobalConfiguration.Configure(WebApiConfig.Register);
            ExceptionlessClient.Default.RegisterWebApi(GlobalConfiguration.Configuration);
            GlobalLogger.Info(nameof(Application_Start));
            DailyStorageScheduler.Start();

            Task.Run(async () =>
            {
                // Wakeup WebService
                using (var client = new HttpClient(new HttpClientHandler { UseProxy = false, }) { Timeout = TimeSpan.FromSeconds(8), })
                {
                    await client.GetAsync("http://localhost:9090");
                }
            });
        }

        protected void Application_BeginRequest(object sender, EventArgs e)
        {
            if (Context.Request.Url.LocalPath == "/")
            {
                HttpContext.Current.Response.Redirect("/swagger");
            }
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

    public class UserAuthorizeAttribute : AuthorizeAttribute
    {
        protected override bool IsAuthorized(System.Web.Http.Controllers.HttpActionContext actionContext)
        {
            var terminal = HttpContext.Current.Request.UserHostAddress.IP();
            return ServiceStartup.GetPrimaryAuthorized(terminal) != null;
        }
    }
}
