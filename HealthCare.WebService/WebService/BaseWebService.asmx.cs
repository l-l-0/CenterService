//------------------------------------------------------------------------------
// @license
// Copyright © 北京盛福瑞安科技有限责任公司. All Rights Reserved.
// 
// Use of this source code is governed by an BSD-style.
//------------------------------------------------------------------------------

using HealthCare.Data;

#pragma warning disable CS1591

namespace HealthCare.WebService.WebService
{
    public class BaseWebService : System.Web.Services.WebService
    {
        protected readonly MongoContext mongo = new MongoContext();
    }
}
