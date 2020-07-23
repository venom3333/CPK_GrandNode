using System;
using System.Collections.Generic;

namespace Grand.Plugin.Payments.Payture.Models
{
    public class PaytureResponse
    {
        public PaytureCommands APIName { get; set; }
        public bool Success { get; set; }
        public string ErrCode { get; set; }
        public string RedirectURL { get; set; }
        public Dictionary<string, string> Attributes { get; set; }
        public dynamic InternalElements { get; set; }
        public string ResponseBodyXML { get; set; }
    }
}
