using Grand.Core.Configuration;

namespace Grand.Plugin.Payments.Payture
{
    public class PayturePaymentSettings : ISettings
    {
        public string Host { get; set; }
        public string MerchantId { get; set; }
        public string Password { get; set; }

        public string DescriptionText { get; set; }
    }
}
