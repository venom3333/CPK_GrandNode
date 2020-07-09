using Grand.Core;
using Grand.Framework.Controllers;
using Grand.Framework.Mvc.Filters;
using Grand.Framework.Security.Authorization;
using Grand.Plugin.Payments.Payture.Models;
using Grand.Services.Configuration;
using Grand.Services.Localization;
using Grand.Services.Security;
using Grand.Services.Stores;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace Grand.Plugin.Payments.Payture.Controllers
{
    [AuthorizeAdmin]
    [Area("Admin")]
    [PermissionAuthorize(PermissionSystemName.PaymentMethods)]
    public class PaymentPaytureController : BasePaymentController
    {
        private readonly IWorkContext _workContext;
        private readonly IStoreService _storeService;
        private readonly ISettingService _settingService;
        private readonly ILocalizationService _localizationService;
        private readonly ILanguageService _languageService;


        public PaymentPaytureController(IWorkContext workContext,
            IStoreService storeService, 
            ISettingService settingService,
            ILocalizationService localizationService,
            ILanguageService languageService)
        {
            _workContext = workContext;
            _storeService = storeService;
            _settingService = settingService;
            _localizationService = localizationService;
            _languageService = languageService;
        }
        
        public async Task<IActionResult> Configure()
        {
            //load settings for a chosen store scope
            var storeScope = await this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var payturePaymentSettings = _settingService.LoadSetting<PayturePaymentSettings>(storeScope);

            var model = new ConfigurationModel();
            model.DescriptionText = payturePaymentSettings.DescriptionText;
            //locales
            await AddLocales(_languageService, model.Locales, (locale, languageId) =>
            {
                locale.DescriptionText = payturePaymentSettings.GetLocalizedSetting(_settingService, x => x.DescriptionText, languageId, "", false, false);
            });
            model.AdditionalFee = payturePaymentSettings.AdditionalFee;
            model.AdditionalFeePercentage = payturePaymentSettings.AdditionalFeePercentage;
            model.ShippableProductRequired = payturePaymentSettings.ShippableProductRequired;

            model.ActiveStoreScopeConfiguration = storeScope;
            if (!String.IsNullOrEmpty(storeScope))
            {
                model.DescriptionText_OverrideForStore = _settingService.SettingExists(payturePaymentSettings, x => x.DescriptionText, storeScope);
                model.AdditionalFee_OverrideForStore = _settingService.SettingExists(payturePaymentSettings, x => x.AdditionalFee, storeScope);
                model.AdditionalFeePercentage_OverrideForStore = _settingService.SettingExists(payturePaymentSettings, x => x.AdditionalFeePercentage, storeScope);
                model.ShippableProductRequired_OverrideForStore = _settingService.SettingExists(payturePaymentSettings, x => x.ShippableProductRequired, storeScope);
            }

            return View("~/Plugins/Payments.Payture/Views/PaymentPayture/Configure.cshtml", model);
        }

        [HttpPost]
        public async Task<IActionResult> Configure(ConfigurationModel model)
        {
            if (!ModelState.IsValid)
                return await Configure();

            //load settings for a chosen store scope
            var storeScope = await this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var payturePaymentSettings = _settingService.LoadSetting<PayturePaymentSettings>(storeScope);

            //save settings
            payturePaymentSettings.DescriptionText = model.DescriptionText;
            payturePaymentSettings.AdditionalFee = model.AdditionalFee;
            payturePaymentSettings.AdditionalFeePercentage = model.AdditionalFeePercentage;
            payturePaymentSettings.ShippableProductRequired = model.ShippableProductRequired;

            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */
            if (model.DescriptionText_OverrideForStore || String.IsNullOrEmpty(storeScope))
                await _settingService.SaveSetting(payturePaymentSettings, x => x.DescriptionText, storeScope, false);
            else if (!String.IsNullOrEmpty(storeScope))
                await _settingService.DeleteSetting(payturePaymentSettings, x => x.DescriptionText, storeScope);

            if (model.AdditionalFee_OverrideForStore || String.IsNullOrEmpty(storeScope))
                await _settingService.SaveSetting(payturePaymentSettings, x => x.AdditionalFee, storeScope, false);
            else if (!String.IsNullOrEmpty(storeScope))
                await _settingService.DeleteSetting(payturePaymentSettings, x => x.AdditionalFee, storeScope);

            if (model.AdditionalFeePercentage_OverrideForStore || String.IsNullOrEmpty(storeScope))
                await _settingService.SaveSetting(payturePaymentSettings, x => x.AdditionalFeePercentage, storeScope, false);
            else if (!String.IsNullOrEmpty(storeScope))
                await _settingService.DeleteSetting(payturePaymentSettings, x => x.AdditionalFeePercentage, storeScope);

            if (model.ShippableProductRequired_OverrideForStore || String.IsNullOrEmpty(storeScope))
                await _settingService.SaveSetting(payturePaymentSettings, x => x.ShippableProductRequired, storeScope, false);
            else if (!String.IsNullOrEmpty(storeScope))
                await _settingService.DeleteSetting(payturePaymentSettings, x => x.ShippableProductRequired, storeScope);

            //now clear settings cache
            await _settingService.ClearCache();

            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return await Configure();
        }
       
    }
}
