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

            model.Host = payturePaymentSettings.Host;
            model.MerchantId = payturePaymentSettings.MerchantId;
            model.Password = payturePaymentSettings.Password;

            model.ActiveStoreScopeConfiguration = storeScope;
            if (!String.IsNullOrEmpty(storeScope))
            {
                model.DescriptionText_OverrideForStore = _settingService.SettingExists(payturePaymentSettings, x => x.DescriptionText, storeScope);

                model.Host_OverrideForStore = _settingService.SettingExists(payturePaymentSettings, x => x.Host, storeScope);
                model.MerchantId_OverrideForStore = _settingService.SettingExists(payturePaymentSettings, x => x.MerchantId, storeScope);
                model.Password_OverrideForStore = _settingService.SettingExists(payturePaymentSettings, x => x.Password, storeScope);
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
            
            payturePaymentSettings.Host = model.Host;
            payturePaymentSettings.MerchantId = model.MerchantId;
            payturePaymentSettings.Password = model.Password;

            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */
            if (model.DescriptionText_OverrideForStore || String.IsNullOrEmpty(storeScope))
                await _settingService.SaveSetting(payturePaymentSettings, x => x.DescriptionText, storeScope, false);
            else if (!String.IsNullOrEmpty(storeScope))
                await _settingService.DeleteSetting(payturePaymentSettings, x => x.DescriptionText, storeScope);

            if(model.Host_OverrideForStore || String.IsNullOrEmpty(storeScope))
                await _settingService.SaveSetting(payturePaymentSettings, x => x.Host, storeScope, false);
            else if (!String.IsNullOrEmpty(storeScope))
                await _settingService.DeleteSetting(payturePaymentSettings, x => x.Host, storeScope);

            if (model.MerchantId_OverrideForStore || String.IsNullOrEmpty(storeScope))
                await _settingService.SaveSetting(payturePaymentSettings, x => x.MerchantId, storeScope, false);
            else if (!String.IsNullOrEmpty(storeScope))
                await _settingService.DeleteSetting(payturePaymentSettings, x => x.MerchantId, storeScope);

            if (model.Password_OverrideForStore || String.IsNullOrEmpty(storeScope))
                await _settingService.SaveSetting(payturePaymentSettings, x => x.Password, storeScope, false);
            else if (!String.IsNullOrEmpty(storeScope))
                await _settingService.DeleteSetting(payturePaymentSettings, x => x.Password, storeScope);

            
            //now clear settings cache
            await _settingService.ClearCache();

            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return await Configure();
        }
       
    }
}
