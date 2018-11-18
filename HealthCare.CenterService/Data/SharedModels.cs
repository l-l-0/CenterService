//------------------------------------------------------------------------------
// @license
// Copyright © 北京盛福瑞安科技有限责任公司. All Rights Reserved.
// 
// Use of this source code is governed by an BSD-style.
//------------------------------------------------------------------------------

using HealthCare.Data;
using HealthCare.Models;

#pragma warning disable CS1591

namespace HealthCare
{
    public class LoginResult
    {
        public LoginCode Code { get; set; }
        public string Jwt { get; set; }
        /// <summary>
        ///     用户的默认登录页面
        /// </summary>
        public string Menu { get; set; }
        public string Ip { get; set; }
    }

    public enum LoginCode
    {
        Ok = 0,
        NotExist = -404,
        UserIsDenied = -403,
        PwdAuthDenied = -401,
        FingerAuthDenied = -402,
        FaceAuthDenied = -406,
        Fail = -405,
        PasswordError = -400,
        DuplicateLogin = -409,
    }


    public class CabinetVersion
    {
        public string Version { get; set; }
        public CabinetDevice[] Cabinets { get; set; }
    }

    public class FingerVersion
    {
        public string Version { get; set; }
        public FingerIdentity[] Fingers { get; set; }
    }
    public class FaceVersion
    {
        public string Version { get; set; }
        public FaceIdentity[] Face { get; set; }
    }
}