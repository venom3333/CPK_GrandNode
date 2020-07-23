using Grand.Core;
using Grand.Core.Domain.Orders;
using Grand.Core.Domain.Payments;
using Grand.Core.Plugins;
using Grand.Plugin.Payments.Payture.Models;
using Grand.Services.Configuration;
using Grand.Services.Localization;
using Grand.Services.Orders;
using Grand.Services.Payments;

using Microsoft.AspNetCore.Http;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

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
        private readonly IHttpContextAccessor _httpContextAccessor;

        #endregion

        #region Ctor

        public PayturePaymentProcessor(PayturePaymentSettings payturePaymentSettings,
            ISettingService settingService, IOrderTotalCalculationService orderTotalCalculationService,
            ILocalizationService localizationService, IWebHelper webHelper, ILanguageService languageService,
            IHttpContextAccessor httpContextAccessor)
        {
            _payturePaymentSettings = payturePaymentSettings;
            _settingService = settingService;
            _orderTotalCalculationService = orderTotalCalculationService;
            _localizationService = localizationService;
            _webHelper = webHelper;
            _languageService = languageService;
            _httpContextAccessor = httpContextAccessor;
        }

        #endregion

        #region Utilites

        #endregion

        #region Methods

        /// <summary>
        /// Helper method for parsing received response (that in XML format)
        /// </summary>
        /// <param name="body">String representation of response body</param>
        /// <param name="command"></param>
        /// <returns>response object</returns>
        private PaytureResponse ParseXMLResponse(string body)
        {
            XElement xmlBody = XElement.Parse(body);
            var attributes = new Dictionary<string, string>();
            var attrs = xmlBody.Attributes();
            if (attrs != null)
            {
                foreach (var val in attrs.Select(n => new KeyValuePair<string, string>(n.Name.LocalName, n.Value)))
                {
                    attributes.Add(val.Key, val.Value);
                }
            }
            var elems = ParseXMLElement(xmlBody);
            var paytureResponse = new PaytureResponse {
                Success = Boolean.Parse(attributes.Where(n => n.Key == "Success").FirstOrDefault().Value),
                ErrCode = attributes.Where(n => n.Key == "ErrCode").FirstOrDefault().Value,
                Attributes = attributes,
                InternalElements = elems,
                ResponseBodyXML = body
            };
            // OnParseResponse( paytureResponse );
            return paytureResponse;
        }

        private dynamic ParseXMLElement(XElement element)
        {
            var elems = element.Elements().ToList();
            if (elems == null || elems.Count() == 0)
                return null;
            var result = elems.Select(n =>
            {
                var internElems = ParseXMLElement(n);
                var elemAttrs = new List<dynamic>();
                foreach (var v in n.Attributes())
                    elemAttrs.Add(new { Name = v.Name.LocalName, Value = v.Value });
                return new { Name = n.Name, Attributes = elemAttrs, InternalElemements = internElems };
            }).ToList();

            return result;
        }


        // {host}/apim/Init
        private HttpRequestMessage PrepareInitRequest(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Post, new Uri($"{_payturePaymentSettings.Host}/apim/Init"));
            var storeLocation = _webHelper.GetStoreLocation();
            var dataDictionary = new Dictionary<string, string> {
                {"SessionType", "Pay"},
                {"OrderId", postProcessPaymentRequest.Order.OrderGuid.ToString()},
                {"Amount", postProcessPaymentRequest.Order.OrderTotal.ToString(CultureInfo.InvariantCulture).Replace(".", "").Replace(",", "")}, // в копейках, без запятых и т.п.
                {"Total", postProcessPaymentRequest.Order.OrderTotal.ToString(CultureInfo.InvariantCulture)},
                {"Url", $"{storeLocation}Plugins/PaymentPayture/ReturnUrlHandler?orderid={{orderid}}&result={{success}}"}

            };
            var dataParameter = GetParameterFromDictionary(dataDictionary);

            var parameters = new Dictionary<string, string> {
                {"Key", _payturePaymentSettings.MerchantId},
                {"Data", dataParameter}
            };

            var encodedContent = new FormUrlEncodedContent(parameters);

            requestMessage.Content = encodedContent;

            return requestMessage;
        }

        // {host}/apim/GetState
        private HttpRequestMessage PrepareGetStateRequest(string orderId)
        {
            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Post, new Uri($"{_payturePaymentSettings.Host}/apim/GetState"));
            
            var parameters = new Dictionary<string, string> {
                {"Key", _payturePaymentSettings.MerchantId},
                {"OrderId", orderId}
            };

            var encodedContent = new FormUrlEncodedContent(parameters);

            requestMessage.Content = encodedContent;

            return requestMessage;
        }

        private string GetParameterFromDictionary(Dictionary<string, string> dataDictionary)
        {
            var result = string.Join(';', dataDictionary.Select(x => $"{x.Key}={x.Value}"));
            return result;
        }

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
            return await Task.FromResult(result);
        }

        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        public async Task PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            using var httpClient = new HttpClient();
            // подготовка запроса Init
            var initRequest = PrepareInitRequest(postProcessPaymentRequest);
            var initResponse = await httpClient.SendAsync(initRequest);
            if (initResponse.IsSuccessStatusCode)
            {
                var initResult = await initResponse.Content.ReadAsStringAsync();

                // парсинг XMLdata
                /*
                 * <Init Success="True" OrderId="865f86a3-d692-b544-4f0d-ae567fca9a67" Amount="12613" SessionId="93148750-2aa1-e3ea-fdb1-66f376929464"/>
                 * 
                 * <Init Success="False" OrderId="865f86a3-d692-b544-4f0d-ae567fca9a67" ErrCode="AMOUNT_ERROR"/>
                 */
                var parsedInitResult = ParseXMLResponse(initResult);
                // получение SessionId по запросу Init
                if (parsedInitResult.Success)
                {
                    if (parsedInitResult.APIName == PaytureCommands.Init)
                    {
                        parsedInitResult.Attributes.TryGetValue("SessionId", out var sessionId);
                        parsedInitResult.Attributes.TryGetValue("Amount", out var amount);

                        var initAmount = postProcessPaymentRequest.Order.OrderTotal.ToString().Replace(",", "").Replace(".", "");

                        if (amount == initAmount)
                        {
                            // редирект ссылка
                            var redirectUri = new Uri($"{_payturePaymentSettings.Host}/apim/Pay?SessionId={sessionId}");
                            var redirectUrl = redirectUri.ToString();
                            // Редирект на полученную ссылку
                            _httpContextAccessor.HttpContext.Response.Redirect(redirectUrl);
                        }
                        else
                        {
                            throw new GrandException($"OrderId {postProcessPaymentRequest.Order.Id} Amounts doesn't match: init {initAmount}, response {amount}");
                        }
                    }
                }
                else
                {
                    throw new GrandException($"OrderId {postProcessPaymentRequest.Order.Id} ErrCode {parsedInitResult.ErrCode}");
                }
            }
            else
            {
                // что-то пошло не так
                var res = await initResponse.Content.ReadAsStringAsync();
                throw new GrandException(res);
            }
            return;
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

            //if (_payturePaymentSettings.ShippableProductRequired && !cart.RequiresShipping())
            //    return true;

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
                0, false);
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

        internal async Task<(bool Success, string State, string TransactionId, string Error)> GetPaymentDetailsAsync(string orderId)
        {
            var state = "";
            var transactionId = "";
            var errCode = "";
            // TODO: запрос в Payture чтобы убедиться в достоверности и получить transactionId
            using var httpClient = new HttpClient();
            HttpRequestMessage getStateRequest = PrepareGetStateRequest(orderId);
            var getStateResponse = await httpClient.SendAsync(getStateRequest);
            bool success;
            if (getStateResponse.IsSuccessStatusCode)
            {
                var getStateResult = await getStateResponse.Content.ReadAsStringAsync();

                // парсинг XMLdata
                /*
                 * <GetState Success="True" OrderId="865f86a3-d692-b544-4f0d-ae567fca9a67" State="Refunded" Forwarded="False" MerchantContract="Merchant" Amount="12613" RRN="003770024290"/>
                 * 
                 * <GetState Success="False" OrderId="865f86a3-d692-b544-4f0d-ae567fca9a67" State="" Forwarded="False" ErrCode="ORDER_NOT_FOUND"/>
                 */
                var parsedGetStateResult = ParseXMLResponse(getStateResult);
                success = parsedGetStateResult.Success;
                // получение SessionId по запросу Init
                if (success)
                {
                    if (parsedGetStateResult.APIName == PaytureCommands.GetState)
                    {
                        parsedGetStateResult.Attributes.TryGetValue("State", out state);
                        parsedGetStateResult.Attributes.TryGetValue("RRN", out transactionId);
                    }
                }
                else
                {
                    errCode = parsedGetStateResult.ErrCode;
                }
            }
            else
            {
                // что-то пошло не так
                var res = await getStateResponse.Content.ReadAsStringAsync();
                throw new GrandException(res);
            }
            return (Success: success, State: state, TransactionId: transactionId, Error: errCode);
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
            var settings = new PayturePaymentSettings {
                DescriptionText = "<p>In cases where an order is placed, an authorized representative will contact you, personally or over telephone, to confirm the order.<br />After the order is confirmed, it will be processed.<br />Orders once confirmed, cannot be cancelled.</p><p>P.S. You can edit this text from admin panel.</p>"
            };
            await _settingService.SaveSetting(settings);

            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Payment.Payture.DescriptionText", "Description");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Payment.Payture.DescriptionText.Hint", "Enter info that will be shown to customers during checkout");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Payment.Payture.PaymentMethodDescription", "Платежный сервис Payture");

            // New code
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Payment.Payture.Host", "Host");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Payment.Payture.Host.Hint", "Адрес хоста Payture, например https://sandbox.payture.com.");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Payment.Payture.MerchantId", "Аккаунт");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Payment.Payture.MerchantId.Hint", "Ваше имя аккаунта, выданное Payture.");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Payment.Payture.Password", "Пароль");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Payment.Payture.Password.Hint", "Ваш пароль, выданный Payture.");


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

            await this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Payment.Payture.Host");
            await this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Payment.Payture.Host.Hint");
            await this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Payment.Payture.MerchantId");
            await this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Payment.Payture.MerchantId.Hint");
            await this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Payment.Payture.Password");
            await this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Payment.Payture.Password.Hint");

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
        public RecurringPaymentType RecurringPaymentType {
            get {
                return RecurringPaymentType.NotSupported;
            }
        }

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType {
            get {
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