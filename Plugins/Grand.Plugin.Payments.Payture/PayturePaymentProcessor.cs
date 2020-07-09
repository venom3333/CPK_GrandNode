using Grand.Core;
using Grand.Core.Domain.Orders;
using Grand.Core.Domain.Payments;
using Grand.Core.Plugins;
using Grand.Services.Configuration;
using Grand.Services.Localization;
using Grand.Services.Orders;
using Grand.Services.Payments;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Grand.Plugin.Payments.Payture
{
    /// <summary>
    /// Payture payment processor
    /// </summary>
    public class PayturePaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Fields

        private readonly PayturePaymentSettings _payturePaymentSettings;
        private readonly ISettingService _settingService;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly ILocalizationService _localizationService;
        private readonly IWebHelper _webHelper;
        private readonly ILanguageService _languageService;

        #endregion

        #region Ctor

        public PayturePaymentProcessor(PayturePaymentSettings payturePaymentSettings,
            ISettingService settingService, IOrderTotalCalculationService orderTotalCalculationService,
            ILocalizationService localizationService, IWebHelper webHelper, ILanguageService languageService)
        {
            _payturePaymentSettings = payturePaymentSettings;
            _settingService = settingService;
            _orderTotalCalculationService = orderTotalCalculationService;
            _localizationService = localizationService;
            _webHelper = webHelper;
            _languageService = languageService;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets a configuration page URL
        /// </summary>
        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/PaymentPayture/Configure";
        }

        /// <summary>
        /// Process a payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public async Task<ProcessPaymentResult> ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            result.NewPaymentStatus = PaymentStatus.Pending;
            return await Task.FromResult(result);
        }

        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        public Task PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            //nothing
            return Task.CompletedTask;
        }

        /// <summary>
        /// Returns a value indicating whether payment method should be hidden during checkout
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>true - hide; false - display.</returns>
        public async Task<bool> HidePaymentMethod(IList<ShoppingCartItem> cart)
        {
            //you can put any logic here
            //for example, hide this payment method if all products in the cart are downloadable
            //or hide this payment method if current customer is from certain country

            if (_payturePaymentSettings.ShippableProductRequired && !cart.RequiresShipping())
                return true;

            return await Task.FromResult(false);
        }

        /// <summary>
        /// Gets additional handling fee
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>Additional handling fee</returns>
        public async Task<decimal> GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            var result = await this.CalculateAdditionalFee(_orderTotalCalculationService, cart,
                _payturePaymentSettings.AdditionalFee, _payturePaymentSettings.AdditionalFeePercentage);
            return result;
        }

        /// <summary>
        /// Captures payment
        /// </summary>
        /// <param name="capturePaymentRequest">Capture payment request</param>
        /// <returns>Capture payment result</returns>
        public async Task<CapturePaymentResult> Capture(CapturePaymentRequest capturePaymentRequest)
        {
            var result = new CapturePaymentResult();
            result.AddError("Capture method not supported");
            return await Task.FromResult(result);
        }

        /// <summary>
        /// Refunds a payment
        /// </summary>
        /// <param name="refundPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public async Task<RefundPaymentResult> Refund(RefundPaymentRequest refundPaymentRequest)
        {
            var result = new RefundPaymentResult();
            result.AddError("Refund method not supported");
            return await Task.FromResult(result);
        }

        /// <summary>
        /// Voids a payment
        /// </summary>
        /// <param name="voidPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public async Task<VoidPaymentResult> Void(VoidPaymentRequest voidPaymentRequest)
        {
            var result = new VoidPaymentResult();
            result.AddError("Void method not supported");
            return await Task.FromResult(result);
        }

        /// <summary>
        /// Process recurring payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public async Task<ProcessPaymentResult> ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            result.AddError("Recurring payment not supported");
            return await Task.FromResult(result);
        }

        /// <summary>
        /// Cancels a recurring payment
        /// </summary>
        /// <param name="cancelPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public async Task<CancelRecurringPaymentResult> CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            var result = new CancelRecurringPaymentResult();
            result.AddError("Recurring payment not supported");
            return await Task.FromResult(result);
        }

        /// <summary>
        /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>Result</returns>
        public async Task<bool> CanRePostProcessPayment(Order order)
        {
            if (order == null)
                throw new ArgumentNullException("order");

            //it's not a redirection payment method. So we always return false
            return await Task.FromResult(false);
        }

        public override async Task Install()
        {
            var settings = new PayturePaymentSettings
            {
                DescriptionText = "<p>In cases where an order is placed, an authorized representative will contact you, personally or over telephone, to confirm the order.<br />After the order is confirmed, it will be processed.<br />Orders once confirmed, cannot be cancelled.</p><p>P.S. You can edit this text from admin panel.</p>"
            };
            await _settingService.SaveSetting(settings);

            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Payment.Payture.DescriptionText", "Description");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Payment.Payture.DescriptionText.Hint", "Enter info that will be shown to customers during checkout");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Payment.Payture.PaymentMethodDescription", "Платежный сервис Payture");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Payment.Payture.AdditionalFee", "Additional fee");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Payment.Payture.AdditionalFee.Hint", "The additional fee.");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Payment.Payture.AdditionalFeePercentage", "Additional fee. Use percentage");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Payment.Payture.AdditionalFeePercentage.Hint", "Determines whether to apply a percentage additional fee to the order total. If not enabled, a fixed value is used.");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Payment.Payture.ShippableProductRequired", "Shippable product required");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Payment.Payture.ShippableProductRequired.Hint", "An option indicating whether shippable products are required in order to display this payment method during checkout.");


            await base.Install();
        }

        public override async Task Uninstall()
        {
            //settings
            await _settingService.DeleteSetting<PayturePaymentSettings>();

            //locales
            await this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Payment.Payture.DescriptionText");
            await this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Payment.Payture.DescriptionText.Hint");
            await this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Payment.Payture.PaymentMethodDescription");
            await this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Payment.Payture.AdditionalFee");
            await this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Payment.Payture.AdditionalFee.Hint");
            await this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Payment.Payture.AdditionalFeePercentage");
            await this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Payment.Payture.AdditionalFeePercentage.Hint");
            await this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Payment.Payture.ShippableProductRequired");
            await this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Payment.Payture.ShippableProductRequired.Hint");

            await base.Uninstall();
        }

        public async Task<IList<string>> ValidatePaymentForm(IFormCollection form)
        {
            var warnings = new List<string>();
            return await Task.FromResult(warnings);
        }

        public async Task<ProcessPaymentRequest> GetPaymentInfo(IFormCollection form)
        {
            var paymentInfo = new ProcessPaymentRequest();
            return await Task.FromResult(paymentInfo);
        }

        public void GetPublicViewComponent(out string viewComponentName)
        {
            viewComponentName = "PaymentPayture";
        }

        #endregion

        #region Properies

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public async Task<bool> SupportCapture()
        {
            return await Task.FromResult(false);
        }

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public async Task<bool> SupportPartiallyRefund()
        {
            return await Task.FromResult(false);
        }

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public async Task<bool> SupportRefund()
        {
            return await Task.FromResult(false);
        }

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public async Task<bool> SupportVoid()
        {
            return await Task.FromResult(false);
        }


        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType
        {
            get
            {
                return RecurringPaymentType.NotSupported;
            }
        }

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType
        {
            get
            {
                return PaymentMethodType.Standard;
            }
        }

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public async Task<bool> SkipPaymentInfo()
        {
            return await Task.FromResult(false);
        }

        public async Task<string> PaymentMethodDescription()
        {
            return await Task.FromResult(_localizationService.GetResource("Plugins.Payment.Payture.PaymentMethodDescription"));
        }
        #endregion

    }
}