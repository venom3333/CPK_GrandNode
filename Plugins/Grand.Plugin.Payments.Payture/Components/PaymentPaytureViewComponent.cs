using Grand.Core;
using Grand.Plugin.Payments.Payture.Models;
using Grand.Services.Configuration;
using Grand.Services.Localization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Grand.Plugin.Payments.Payture.Components
{
    public class PaymentPaytureViewComponent : ViewComponent
    {
        private readonly IWorkContext _workContext;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;

        public PaymentPaytureViewComponent(IWorkContext workContext,   
            ISettingService settingService,
            IStoreContext storeContext)
        {
            _workContext = workContext;
            _settingService = settingService;
            _storeContext = storeContext;

        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var payturePaymentSettings = _settingService.LoadSetting<PayturePaymentSettings>(_storeContext.CurrentStore.Id);

            var model = new PaymentInfoModel
            {
                DescriptionText = payturePaymentSettings.GetLocalizedSetting(_settingService, x => x.DescriptionText, _workContext.WorkingLanguage.Id, _storeContext.CurrentStore.Id)
            };
            return View("~/Plugins/Payments.Payture/Views/PaymentPayture/PaymentInfo.cshtml", await Task.FromResult(model));
        }
    }
}