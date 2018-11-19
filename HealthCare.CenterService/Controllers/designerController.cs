using HealthCare.Data;
using MongoDB.Driver;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;

#pragma warning disable CS1591

namespace HealthCare.CenterService.Controllers
{
    public class designerController : BaseController
    {
        public class DesignerData : RenderDetail
        {
            public string Component { get; set; }
            public string DataType { get; set; }
            public string DisplayName { get; set; }
        }

        /// <summary>
        ///     提交设计器模板
        /// </summary>
        [HttpPut]
        [ActionName("modify-template-for-designer")]
        public async Task<string> ModifyTemplateForDesignerAsync([FromBody] DesignerData data)
        {
            var template = $"{Global.Hospital}@{data.Component}@{data.DataType}";
            var instance = new RenderDetail
            {
                Height = data.Height,
                Width = data.Width,
                Rendering = data.Rendering,
            };
            if (mongo.DesignerTemplateCollection.AsQueryable().Any(t => t.UniqueId == template))
            {
                await mongo.DesignerTemplateCollection.UpdateOneAsync(t => t.UniqueId == template, Builders<DesignerTemplate>.Update.Push(t => t.RenderDetails, instance).Set(t => t.DisplayName, data.DisplayName));
            }
            else
            {
                var t = new DesignerTemplate
                {
                    UniqueId = template,
                    DisplayName = data.DisplayName,
                    RenderDetails = new List<RenderDetail> { instance, }
                };
                await mongo.DesignerTemplateCollection.InsertOneAsync(t);
            }
            return template;
        }

        /// <summary>
        ///     查询模板，如果未指定版本则返回最新一个
        /// </summary>
        [HttpGet]
        [ActionName("search-designer-template")]
        public dynamic SearchDesignerTemplateAsync(string template, int index = -1)
        {
            var designer = mongo.DesignerTemplateCollection.AsQueryable().Where(t => t.UniqueId == template);
            var lq = designer.SelectMany(t => t.RenderDetails);
            var count = lq.Count();
            var skip = (index < 0 ? count : index) - 1;
            var data = lq.Skip(skip < 0 ? 0 : skip).Take(1);
            return new
            {
                Count = count,
                TemplateName = designer.FirstOrDefault().DisplayName,
                Data = data.FirstOrDefault(),
            };
        }

        public class DesignerTemplateProfile
        {
            public string UniqueId { get; set; }
            public string DisplayName { get; set; }
            public Int32 DisplayOrder { get; set; }
        }
        /// <summary>
        ///     查询所有模板
        /// </summary>
        [HttpGet]
        [ActionName("search-all-designer-templates")]
        public DesignerTemplateProfile[] SearchAllDesignerTemplates()
        {
            var lq = mongo.DesignerTemplateCollection.AsQueryable().Where(d => !d.IsDisabled).ToList();
            return lq.Select(d =>
           {
               return new DesignerTemplateProfile
               {
                   UniqueId = d.UniqueId,
                   DisplayName = d.DisplayName,
                   DisplayOrder = d.DisplayOrder
               };
           }).ToArray();
        }

        /// <summary>
        ///     查询一条随机的红处方打印数据
        /// </summary>
        [HttpGet]
        [ActionName("search-random-print-red-prescription")]
        public ngController.PrescriptionPrintProfile SearchRandomPrintRedPrescription()
        {
            var count = mongo.PrescriptionCollection.AsQueryable().Where(p => !p.IsDisabled).Count();
            var index = new Random().Next(0, count);
            var pId = mongo.PrescriptionCollection.AsQueryable().Where(p => !p.IsDisabled).Skip(index <= 0 ? 0 : index - 1).Take(1).Select(p => p.UniqueId).FirstOrDefault();
            return new ngController().SearchPrintRedPrescription(new string[] { pId })[0];
        }
    }
}