//------------------------------------------------------------------------------
// @license
// Copyright © 北京盛福瑞安科技有限责任公司. All Rights Reserved.
// 
// Use of this source code is governed by an BSD-style.
//------------------------------------------------------------------------------

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Cors;
using System.Web.Http.Filters;

namespace HealthCare.CenterService
{
    /// <summary>
    ///     WebApi 配置，Cors，Filters，Routes 等
    /// </summary>
    public static class WebApiConfig
    {
        /// <summary>
        ///     Cors，Filters，Routes
        /// </summary>
        /// <param name="config"></param>
        public static void Register(HttpConfiguration config)
        {
            // Web API configuration and services
            config.EnableCors(new EnableCorsAttribute(origins: "*", headers: "*", methods: "*"));
            config.Formatters.Remove(config.Formatters.XmlFormatter);
            config.Formatters.JsonFormatter.SerializerSettings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Formatting = Formatting.None,
                NullValueHandling = NullValueHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
            };

            config.Filters.Add(new ActionMonitorAttribute());
            config.Filters.Add(new ActionUnhandledErrorAttribute());

            // Web API routes
            config.MapHttpAttributeRoutes();
            config.Routes.MapHttpRoute(name: "DefaultApi", routeTemplate: "api/{controller}/{action}", defaults: new { });
        }

        /// <summary>
        ///     <see cref="ActionMonitorAttribute" />
        /// </summary>
        public class ActionMonitorAttribute : ActionFilterAttribute
        {
            /// <summary>
            ///     请求中添加当前时间，在返回时计算此次请求耗时
            /// </summary>
            public override void OnActionExecuting(HttpActionContext actionContext)
            {
                HttpContext.Current.Items.Add("WebKit", DateTime.Now);
                ServiceStartup.RefreshAuthorized(HttpContext.Current.Request.UserHostAddress);
                base.OnActionExecuting(actionContext);
            }

            /// <summary>
            ///     Called by the ASP.NET MVC framework after the action method executes.
            /// </summary>
            public override void OnActionExecuted(HttpActionExecutedContext actionExecutedContext)
            {
                var span = DateTime.Now - (DateTime)HttpContext.Current.Items["WebKit"];
                var info = $"{HttpContext.Current.Request.UserHostAddress},{HttpContext.Current.Request.ContentType}\t[{HttpContext.Current.Request.HttpMethod}]\t{{{HttpContext.Current.Request.RawUrl}}}\t[{span}]";
                object value = null;
                if (actionExecutedContext.Exception != null)
                {
                    value = new { Exception = actionExecutedContext.Exception.Message, InnerException = actionExecutedContext.Exception.InnerException?.Message, };
                    var body = BodyString(HttpContext.Current.Request.InputStream);
                    if (!string.IsNullOrEmpty(body))
                    {
                        info = $"{info}{Environment.NewLine}{body}";
                    }
                    Global.MonitorLogger.Error(info, actionExecutedContext.Exception);
                }
                else
                {
                    value = JsonConvert.DeserializeObject(actionExecutedContext.Response.Content.ReadAsStringAsync().Result);
                    Global.MonitorLogger.Info(info);
                }
                actionExecutedContext.Response = actionExecutedContext.Request.CreateResponse(HttpStatusCode.OK, new { value, span }, JsonMediaTypeFormatter.DefaultMediaType);
                base.OnActionExecuted(actionExecutedContext);

                // 从请求体中获取数据
                string BodyString(Stream stream)
                {
                    if (stream == null)
                    {
                        return null;
                    }

                    using (var ms = new MemoryStream())
                    {
                        if (stream.CanSeek)
                        {
                            stream.Position = 0;
                            var pos = (int)ms.Position;
                            var length = (int)(stream.Length - stream.Position) + pos;
                            ms.SetLength(length);
                            while (pos < length)
                            {
                                pos += stream.Read(ms.GetBuffer(), pos, length - pos);
                            }
                        }
                        else
                        {
                            stream.CopyTo(ms);
                        }
                        using (var streamReader = new StreamReader(ms))
                        {
                            return streamReader.ReadToEnd();
                        }
                    }
                }
            }
        }

        /// <summary>
        ///     全局抓取异常
        /// </summary>
        public class ActionUnhandledErrorAttribute : ExceptionFilterAttribute
        {
            /// <summary>
            ///     异常不能导致服务器挂
            /// </summary>
            public override void OnException(HttpActionExecutedContext filterContext)
            {
                var span = DateTime.Now - (DateTime)HttpContext.Current.Items["WebKit"];
                var info = $"{HttpContext.Current.Request.UserHostAddress},{HttpContext.Current.Request.ContentType}\t[{HttpContext.Current.Request.HttpMethod}]\t{{{HttpContext.Current.Request.RawUrl}}}\t[{span}]";
                Global.MonitorLogger.Error(info, filterContext.Exception);

                filterContext.Response = new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonConvert.SerializeObject(new
                    {
                        value = new { Exception = filterContext.Exception.Message, InnerException = filterContext.Exception.InnerException?.Message, },
                        span,
                    })),
                };
                filterContext.Exception = null;
                base.OnException(filterContext);
            }
        }
    }
}