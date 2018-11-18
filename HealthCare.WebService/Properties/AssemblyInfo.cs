//------------------------------------------------------------------------------
// @license
// Copyright © 北京盛福瑞安科技有限责任公司. All Rights Reserved.
// 
// Use of this source code is governed by an BSD-style.
//------------------------------------------------------------------------------

using log4net.Config;
using System.Reflection;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("HealthCare.WebService")]
[assembly: AssemblyDescription("第三方对接服务，使用 WebService")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("北京盛福瑞安科技有限责任公司")]
[assembly: AssemblyProduct("HealthCare.WebService")]
[assembly: AssemblyCopyright("Copyright © 北京盛福瑞安科技有限责任公司 2018")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: XmlConfigurator(Watch = true, ConfigFile = "log4net.config")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("a12f115b-8c3a-4cf7-a6e8-18a6e012b683")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version 
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Revision and Build Numbers 
// by using the '*' as shown below:
[assembly: AssemblyVersion("20.18.*")]
