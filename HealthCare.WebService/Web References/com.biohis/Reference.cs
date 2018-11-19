﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

// 
// This source code was auto-generated by Microsoft.VSDesigner, Version 4.0.30319.42000.
// 
#pragma warning disable 1591

namespace HealthCare.WebService.com.biohis {
    using System;
    using System.Web.Services;
    using System.Diagnostics;
    using System.Web.Services.Protocols;
    using System.Xml.Serialization;
    using System.ComponentModel;
    
    
    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Web.Services", "4.7.2556.0")]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Web.Services.WebServiceBindingAttribute(Name="MedicineSoap11Binding", Namespace="http://MedicineCabinetService.biointerface.com")]
    public partial class Medicine : System.Web.Services.Protocols.SoapHttpClientProtocol {
        
        private System.Threading.SendOrPostCallback getEmpOperationCompleted;
        
        private System.Threading.SendOrPostCallback getDrugOperationCompleted;
        
        private System.Threading.SendOrPostCallback getInpatientOperationCompleted;
        
        private System.Threading.SendOrPostCallback getStockOperationCompleted;
        
        private bool useDefaultCredentialsSetExplicitly;
        
        /// <remarks/>
        public Medicine() {
            this.Url = global::HealthCare.WebService.Properties.Settings.Default.HealthCare_WebService_com_biohis_Medicine;
            if ((this.IsLocalFileSystemWebService(this.Url) == true)) {
                this.UseDefaultCredentials = true;
                this.useDefaultCredentialsSetExplicitly = false;
            }
            else {
                this.useDefaultCredentialsSetExplicitly = true;
            }
        }
        
        public new string Url {
            get {
                return base.Url;
            }
            set {
                if ((((this.IsLocalFileSystemWebService(base.Url) == true) 
                            && (this.useDefaultCredentialsSetExplicitly == false)) 
                            && (this.IsLocalFileSystemWebService(value) == false))) {
                    base.UseDefaultCredentials = false;
                }
                base.Url = value;
            }
        }
        
        public new bool UseDefaultCredentials {
            get {
                return base.UseDefaultCredentials;
            }
            set {
                base.UseDefaultCredentials = value;
                this.useDefaultCredentialsSetExplicitly = true;
            }
        }
        
        /// <remarks/>
        public event getEmpCompletedEventHandler getEmpCompleted;
        
        /// <remarks/>
        public event getDrugCompletedEventHandler getDrugCompleted;
        
        /// <remarks/>
        public event getInpatientCompletedEventHandler getInpatientCompleted;
        
        /// <remarks/>
        public event getStockCompletedEventHandler getStockCompleted;
        
        /// <remarks/>
        [System.Web.Services.Protocols.SoapDocumentMethodAttribute("urn:getEmp", RequestNamespace="http://MedicineCabinetService.biointerface.com", ResponseNamespace="http://MedicineCabinetService.biointerface.com", Use=System.Web.Services.Description.SoapBindingUse.Literal, ParameterStyle=System.Web.Services.Protocols.SoapParameterStyle.Wrapped)]
        [return: System.Xml.Serialization.XmlElementAttribute("return", IsNullable=true)]
        [System.Web.Services.Protocols.SoapHeaderAttribute("header")]
        public string getEmp([System.Xml.Serialization.XmlElementAttribute(IsNullable=true)] string workNO) {
            object[] results = this.Invoke("getEmp", new object[] {
                        workNO});
            return ((string)(results[0]));
        }
        
        /// <remarks/>
        public void getEmpAsync(string workNO) {
            this.getEmpAsync(workNO, null);
        }
        
        /// <remarks/>
        public void getEmpAsync(string workNO, object userState) {
            if ((this.getEmpOperationCompleted == null)) {
                this.getEmpOperationCompleted = new System.Threading.SendOrPostCallback(this.OngetEmpOperationCompleted);
            }
            this.InvokeAsync("getEmp", new object[] {
                        workNO}, this.getEmpOperationCompleted, userState);
        }
        
        private void OngetEmpOperationCompleted(object arg) {
            if ((this.getEmpCompleted != null)) {
                System.Web.Services.Protocols.InvokeCompletedEventArgs invokeArgs = ((System.Web.Services.Protocols.InvokeCompletedEventArgs)(arg));
                this.getEmpCompleted(this, new getEmpCompletedEventArgs(invokeArgs.Results, invokeArgs.Error, invokeArgs.Cancelled, invokeArgs.UserState));
            }
        }
        
        /// <remarks/>
        [System.Web.Services.Protocols.SoapDocumentMethodAttribute("urn:getDrug", RequestNamespace="http://MedicineCabinetService.biointerface.com", ResponseNamespace="http://MedicineCabinetService.biointerface.com", Use=System.Web.Services.Description.SoapBindingUse.Literal, ParameterStyle=System.Web.Services.Protocols.SoapParameterStyle.Wrapped)]
        [return: System.Xml.Serialization.XmlElementAttribute("return", IsNullable=true)]
        [System.Web.Services.Protocols.SoapHeaderAttribute("header")]
        public string getDrug(long drugId, [System.Xml.Serialization.XmlIgnoreAttribute()] bool drugIdSpecified) {
            object[] results = this.Invoke("getDrug", new object[] {
                        drugId,
                        drugIdSpecified});
            return ((string)(results[0]));
        }
        
        /// <remarks/>
        public void getDrugAsync(long drugId, bool drugIdSpecified) {
            this.getDrugAsync(drugId, drugIdSpecified, null);
        }
        
        /// <remarks/>
        public void getDrugAsync(long drugId, bool drugIdSpecified, object userState) {
            if ((this.getDrugOperationCompleted == null)) {
                this.getDrugOperationCompleted = new System.Threading.SendOrPostCallback(this.OngetDrugOperationCompleted);
            }
            this.InvokeAsync("getDrug", new object[] {
                        drugId,
                        drugIdSpecified}, this.getDrugOperationCompleted, userState);
        }
        
        private void OngetDrugOperationCompleted(object arg) {
            if ((this.getDrugCompleted != null)) {
                System.Web.Services.Protocols.InvokeCompletedEventArgs invokeArgs = ((System.Web.Services.Protocols.InvokeCompletedEventArgs)(arg));
                this.getDrugCompleted(this, new getDrugCompletedEventArgs(invokeArgs.Results, invokeArgs.Error, invokeArgs.Cancelled, invokeArgs.UserState));
            }
        }
        
        /// <remarks/>
        [System.Web.Services.Protocols.SoapDocumentMethodAttribute("urn:getInpatient", RequestNamespace="http://MedicineCabinetService.biointerface.com", ResponseNamespace="http://MedicineCabinetService.biointerface.com", Use=System.Web.Services.Description.SoapBindingUse.Literal, ParameterStyle=System.Web.Services.Protocols.SoapParameterStyle.Wrapped)]
        [return: System.Xml.Serialization.XmlElementAttribute("return", IsNullable=true)]
        [System.Web.Services.Protocols.SoapHeaderAttribute("header")]
        public string getInpatient([System.Xml.Serialization.XmlElementAttribute(IsNullable=true)] string inpatientNo) {
            object[] results = this.Invoke("getInpatient", new object[] {
                        inpatientNo});
            return ((string)(results[0]));
        }
        
        /// <remarks/>
        public void getInpatientAsync(string inpatientNo) {
            this.getInpatientAsync(inpatientNo, null);
        }
        
        /// <remarks/>
        public void getInpatientAsync(string inpatientNo, object userState) {
            if ((this.getInpatientOperationCompleted == null)) {
                this.getInpatientOperationCompleted = new System.Threading.SendOrPostCallback(this.OngetInpatientOperationCompleted);
            }
            this.InvokeAsync("getInpatient", new object[] {
                        inpatientNo}, this.getInpatientOperationCompleted, userState);
        }
        
        private void OngetInpatientOperationCompleted(object arg) {
            if ((this.getInpatientCompleted != null)) {
                System.Web.Services.Protocols.InvokeCompletedEventArgs invokeArgs = ((System.Web.Services.Protocols.InvokeCompletedEventArgs)(arg));
                this.getInpatientCompleted(this, new getInpatientCompletedEventArgs(invokeArgs.Results, invokeArgs.Error, invokeArgs.Cancelled, invokeArgs.UserState));
            }
        }
        
        /// <remarks/>
        [System.Web.Services.Protocols.SoapDocumentMethodAttribute("urn:getStock", RequestNamespace="http://MedicineCabinetService.biointerface.com", ResponseNamespace="http://MedicineCabinetService.biointerface.com", Use=System.Web.Services.Description.SoapBindingUse.Literal, ParameterStyle=System.Web.Services.Protocols.SoapParameterStyle.Wrapped)]
        [return: System.Xml.Serialization.XmlElementAttribute("return", IsNullable=true)]
        [System.Web.Services.Protocols.SoapHeaderAttribute("header")]
        public string getStock([System.Xml.Serialization.XmlElementAttribute(IsNullable=true)] string stockNO) {
            object[] results = this.Invoke("getStock", new object[] {
                        stockNO});
            return ((string)(results[0]));
        }
        
        /// <remarks/>
        public void getStockAsync(string stockNO) {
            this.getStockAsync(stockNO, null);
        }
        
        /// <remarks/>
        public void getStockAsync(string stockNO, object userState) {
            if ((this.getStockOperationCompleted == null)) {
                this.getStockOperationCompleted = new System.Threading.SendOrPostCallback(this.OngetStockOperationCompleted);
            }
            this.InvokeAsync("getStock", new object[] {
                        stockNO}, this.getStockOperationCompleted, userState);
        }
        
        private void OngetStockOperationCompleted(object arg) {
            if ((this.getStockCompleted != null)) {
                System.Web.Services.Protocols.InvokeCompletedEventArgs invokeArgs = ((System.Web.Services.Protocols.InvokeCompletedEventArgs)(arg));
                this.getStockCompleted(this, new getStockCompletedEventArgs(invokeArgs.Results, invokeArgs.Error, invokeArgs.Cancelled, invokeArgs.UserState));
            }
        }
        
        /// <remarks/>
        public new void CancelAsync(object userState) {
            base.CancelAsync(userState);
        }
        
        private bool IsLocalFileSystemWebService(string url) {
            if (((url == null) 
                        || (url == string.Empty))) {
                return false;
            }
            System.Uri wsUri = new System.Uri(url);
            if (((wsUri.Port >= 1024) 
                        && (string.Compare(wsUri.Host, "localHost", System.StringComparison.OrdinalIgnoreCase) == 0))) {
                return true;
            }
            return false;
        }
    }
    
    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Web.Services", "4.7.2556.0")]
    public delegate void getEmpCompletedEventHandler(object sender, getEmpCompletedEventArgs e);
    
    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Web.Services", "4.7.2556.0")]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    public partial class getEmpCompletedEventArgs : System.ComponentModel.AsyncCompletedEventArgs {
        
        private object[] results;
        
        internal getEmpCompletedEventArgs(object[] results, System.Exception exception, bool cancelled, object userState) : 
                base(exception, cancelled, userState) {
            this.results = results;
        }
        
        /// <remarks/>
        public string Result {
            get {
                this.RaiseExceptionIfNecessary();
                return ((string)(this.results[0]));
            }
        }
    }
    
    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Web.Services", "4.7.2556.0")]
    public delegate void getDrugCompletedEventHandler(object sender, getDrugCompletedEventArgs e);
    
    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Web.Services", "4.7.2556.0")]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    public partial class getDrugCompletedEventArgs : System.ComponentModel.AsyncCompletedEventArgs {
        
        private object[] results;
        
        internal getDrugCompletedEventArgs(object[] results, System.Exception exception, bool cancelled, object userState) : 
                base(exception, cancelled, userState) {
            this.results = results;
        }
        
        /// <remarks/>
        public string Result {
            get {
                this.RaiseExceptionIfNecessary();
                return ((string)(this.results[0]));
            }
        }
    }
    
    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Web.Services", "4.7.2556.0")]
    public delegate void getInpatientCompletedEventHandler(object sender, getInpatientCompletedEventArgs e);
    
    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Web.Services", "4.7.2556.0")]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    public partial class getInpatientCompletedEventArgs : System.ComponentModel.AsyncCompletedEventArgs {
        
        private object[] results;
        
        internal getInpatientCompletedEventArgs(object[] results, System.Exception exception, bool cancelled, object userState) : 
                base(exception, cancelled, userState) {
            this.results = results;
        }
        
        /// <remarks/>
        public string Result {
            get {
                this.RaiseExceptionIfNecessary();
                return ((string)(this.results[0]));
            }
        }
    }
    
    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Web.Services", "4.7.2556.0")]
    public delegate void getStockCompletedEventHandler(object sender, getStockCompletedEventArgs e);
    
    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Web.Services", "4.7.2556.0")]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    public partial class getStockCompletedEventArgs : System.ComponentModel.AsyncCompletedEventArgs {
        
        private object[] results;
        
        internal getStockCompletedEventArgs(object[] results, System.Exception exception, bool cancelled, object userState) : 
                base(exception, cancelled, userState) {
            this.results = results;
        }
        
        /// <remarks/>
        public string Result {
            get {
                this.RaiseExceptionIfNecessary();
                return ((string)(this.results[0]));
            }
        }
    }
}

#pragma warning restore 1591