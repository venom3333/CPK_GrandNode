using Grand.Framework.Localization;
using Grand.Framework.Mvc.ModelBinding;
using Grand.Framework.Mvc.Models;
using System.Collections.Generic;

namespace Grand.Plugin.Payments.Payture.Models
{
    public class ConfigurationModel : BaseGrandModel, ILocalizedModel<ConfigurationModel.ConfigurationLocalizedModel>
    {
        public ConfigurationModel()
        {
            Locales = new List<ConfigurationLocalizedModel>();
        }

        public string ActiveStoreScopeConfiguration { get; set; }

        // New code
        [GrandResourceDisplayName("Plugins.Payment.Payture.Host")]
        public string Host { get; set; }
        public bool Host_OverrideForStore { get; set; }

        [GrandResourceDisplayName("Plugins.Payment.Payture.MerchantId")]
        public string MerchantId { get; set; }
        public bool MerchantId_OverrideForStore { get; set; }

        [GrandResourceDisplayName("Plugins.Payment.Payture.Password")]
        public string Password { get; set; }
        public bool Password_OverrideForStore { get; set; }

        [GrandResourceDisplayName("Plugins.Payment.Payture.DescriptionText")]
        public string DescriptionText { get; set; }
        public bool DescriptionText_OverrideForStore { get; set; }

        public IList<ConfigurationLocalizedModel> Locales { get; set; }

        #region Nested class

        public partial class ConfigurationLocalizedModel : ILocalizedModelLocal
        {
            public string LanguageId { get; set; }
            
            [GrandResourceDisplayName("Plugins.Payment.Payture.DescriptionText")]
            public string DescriptionText { get; set; }
        }

        #endregion
    }
}