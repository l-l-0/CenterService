﻿//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

namespace HealthCare.WebService.ServiceReference2 {
    
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ServiceModel.ServiceContractAttribute(ConfigurationName="ServiceReference2.HISWebServiceSoap")]
    public interface HISWebServiceSoap {
        
        // CODEGEN: 命名空间 http://tempuri.org/ 的元素名称 xml 以后生成的消息协定未标记为 nillable
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/reciveData", ReplyAction="*")]
        HealthCare.WebService.ServiceReference2.reciveDataResponse reciveData(HealthCare.WebService.ServiceReference2.reciveDataRequest request);
        
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/testReciveData", ReplyAction="*")]
        bool testReciveData();
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(IsWrapped=false)]
    public partial class reciveDataRequest {
        
        [System.ServiceModel.MessageBodyMemberAttribute(Name="reciveData", Namespace="http://tempuri.org/", Order=0)]
        public HealthCare.WebService.ServiceReference2.reciveDataRequestBody Body;
        
        public reciveDataRequest() {
        }
        
        public reciveDataRequest(HealthCare.WebService.ServiceReference2.reciveDataRequestBody Body) {
            this.Body = Body;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.Runtime.Serialization.DataContractAttribute(Namespace="http://tempuri.org/")]
    public partial class reciveDataRequestBody {
        
        [System.Runtime.Serialization.DataMemberAttribute(EmitDefaultValue=false, Order=0)]
        public string xml;
        
        public reciveDataRequestBody() {
        }
        
        public reciveDataRequestBody(string xml) {
            this.xml = xml;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(IsWrapped=false)]
    public partial class reciveDataResponse {
        
        [System.ServiceModel.MessageBodyMemberAttribute(Name="reciveDataResponse", Namespace="http://tempuri.org/", Order=0)]
        public HealthCare.WebService.ServiceReference2.reciveDataResponseBody Body;
        
        public reciveDataResponse() {
        }
        
        public reciveDataResponse(HealthCare.WebService.ServiceReference2.reciveDataResponseBody Body) {
            this.Body = Body;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.Runtime.Serialization.DataContractAttribute(Namespace="http://tempuri.org/")]
    public partial class reciveDataResponseBody {
        
        [System.Runtime.Serialization.DataMemberAttribute(Order=0)]
        public bool reciveDataResult;
        
        public reciveDataResponseBody() {
        }
        
        public reciveDataResponseBody(bool reciveDataResult) {
            this.reciveDataResult = reciveDataResult;
        }
    }
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    public interface HISWebServiceSoapChannel : HealthCare.WebService.ServiceReference2.HISWebServiceSoap, System.ServiceModel.IClientChannel {
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    public partial class HISWebServiceSoapClient : System.ServiceModel.ClientBase<HealthCare.WebService.ServiceReference2.HISWebServiceSoap>, HealthCare.WebService.ServiceReference2.HISWebServiceSoap {
        
        public HISWebServiceSoapClient() {
        }
        
        public HISWebServiceSoapClient(string endpointConfigurationName) : 
                base(endpointConfigurationName) {
        }
        
        public HISWebServiceSoapClient(string endpointConfigurationName, string remoteAddress) : 
                base(endpointConfigurationName, remoteAddress) {
        }
        
        public HISWebServiceSoapClient(string endpointConfigurationName, System.ServiceModel.EndpointAddress remoteAddress) : 
                base(endpointConfigurationName, remoteAddress) {
        }
        
        public HISWebServiceSoapClient(System.ServiceModel.Channels.Binding binding, System.ServiceModel.EndpointAddress remoteAddress) : 
                base(binding, remoteAddress) {
        }
        
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        HealthCare.WebService.ServiceReference2.reciveDataResponse HealthCare.WebService.ServiceReference2.HISWebServiceSoap.reciveData(HealthCare.WebService.ServiceReference2.reciveDataRequest request) {
            return base.Channel.reciveData(request);
        }
        
        public bool reciveData(string xml) {
            HealthCare.WebService.ServiceReference2.reciveDataRequest inValue = new HealthCare.WebService.ServiceReference2.reciveDataRequest();
            inValue.Body = new HealthCare.WebService.ServiceReference2.reciveDataRequestBody();
            inValue.Body.xml = xml;
            HealthCare.WebService.ServiceReference2.reciveDataResponse retVal = ((HealthCare.WebService.ServiceReference2.HISWebServiceSoap)(this)).reciveData(inValue);
            return retVal.Body.reciveDataResult;
        }
        
        public bool testReciveData() {
            return base.Channel.testReciveData();
        }
    }
}