using Grand.Core;
using Grand.Core.Domain.Orders;
using Grand.Core.Domain.Payments;
using Grand.Framework.Controllers;
using Grand.Framework.Mvc.Filters;
using Grand.Framework.Security.Authorization;
using Grand.Plugin.Payments.Payture.Models;
using Grand.Services.Configuration;
using Grand.Services.Localization;
using Grand.Services.Orders;
using Grand.Services.Payments;
using Grand.Services.Security;
using Grand.Services.Stores;

using Microsoft.AspNetCore.Mvc;

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Linq;

namespace Grand.Plugin.Payments.Payture.Controllers
{
    public class PaymentPaytureController : BasePaymentController
    {
        private readonly IWorkContext _workContext;
        private readonly IStoreService _storeService;
        private readonly ISettingService _settingService;
        private readonly ILocalizationService _localizationService;
        private readonly ILanguageService _languageService;
        private readonly IPaymentService _paymentService;
        private readonly IOrderService _orderService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly PaymentSettings _paymentSettings;


        public PaymentPaytureController(IWorkContext workContext,
            IStoreService storeService,
            ISettingService settingService,
            ILocalizationService localizationService,
            ILanguageService languageService,
            IPaymentService paymentService,
            IOrderService orderService,
            IOrderProcessingService orderProcessingService,
            PaymentSettings paymentSettings)
        {
            _workContext = workContext;
            _storeService = storeService;
            _settingService = settingService;
            _localizationService = localizationService;
            _languageService = languageService;
            _paymentService = paymentService;
            _orderService = orderService;
            _orderProcessingService = orderProcessingService;
            _paymentSettings = paymentSettings;
        }

        [AuthorizeAdmin]
        [Area("Admin")]
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
        [AuthorizeAdmin]
        [Area("Admin")]
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

            if (model.Host_OverrideForStore || String.IsNullOrEmpty(storeScope))
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

        public async Task<IActionResult> ReturnUrlHandler(string result, string orderid)
        {
            var processor = _paymentService.LoadPaymentMethodBySystemName("Payments.Payture") as PayturePaymentProcessor;
            if (processor == null ||
                !processor.IsPaymentMethodActive(_paymentSettings) || !processor.PluginDescriptor.Installed)
                throw new GrandException("Payture module cannot be loaded");


            Guid orderNumberGuid = Guid.Empty;
            try
            {
                orderNumberGuid = new Guid(orderid);
            }
            catch { }
            Order order = await _orderService.GetOrderByGuid(orderNumberGuid);
            if (order == null)
            {
                return RedirectToAction("Index", "Home", new { area = "" });
            }

            bool.TryParse(result, out bool isSuccess);
            if (!isSuccess)
            {
                //order note
                await _orderService.InsertOrderNote(new OrderNote {
                    Note = "Return url handler received 'NOT SUCCESS'",
                    OrderId = order.Id,
                    DisplayToCustomer = false,
                    CreatedOnUtc = DateTime.UtcNow
                });
                return RedirectToAction("Index", "Home", new { area = "" });
            }

            var (Success, State, TransactionId, Error) = await processor.GetPaymentDetailsAsync(orderid);
            if (Success)
            {
                //order note
                await _orderService.InsertOrderNote(new OrderNote {
                    Note = $"Success: orderId {order.Id}, state {State}, transactionId {TransactionId}",
                    DisplayToCustomer = false,
                    CreatedOnUtc = DateTime.UtcNow,
                    OrderId = order.Id,
                });

                // load settings for a chosen store scope
                // var storeScope = await this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
                // var payturePaymentSettings = _settingService.LoadSetting<PayturePaymentSettings>(storeScope);

                if (State == "Charged")
                {
                    if (await _orderProcessingService.CanMarkOrderAsPaid(order))
                    {
                        order.AuthorizationTransactionId = TransactionId;
                        await _orderService.UpdateOrder(order);
                        await _orderProcessingService.MarkOrderAsPaid(order);
                    }
                }

                return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
            }
            else
            {
                //order note
                await _orderService.InsertOrderNote(new OrderNote {
                    Note = $"Error: {Error}",
                    DisplayToCustomer = false,
                    CreatedOnUtc = DateTime.UtcNow,
                    OrderId = order.Id,
                });
                return RedirectToAction("Index", "Home", new { area = "" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> NotificationHandler([FromBody] string body)
        {
            var processor = _paymentService.LoadPaymentMethodBySystemName("Payments.Payture") as PayturePaymentProcessor;
            if (processor == null ||
                !processor.IsPaymentMethodActive(_paymentSettings) || !processor.PluginDescriptor.Installed)
                throw new GrandException("Payture module cannot be loaded");

            var decodedBody = WebUtility.UrlDecode(body);
            NameValueCollection attributes = HttpUtility.ParseQueryString(decodedBody);

            var acceptedNotifications = new List<string> { "MerchantPay", "EnginePaySuccess" };

            var notification = attributes.Get("Notification");
            if (!acceptedNotifications.Contains(notification))
            {
                return BadRequest();
            }
            var orderId = attributes.Get("OrderId");
            var successString = attributes.Get("Success");
            bool.TryParse(successString, out bool notificationSuccess);
            var amount = attributes.Get("Amount");
            var errCode = "";
            if (!notificationSuccess)
            {
                errCode = attributes.Get("ErrCode");
            }

            Guid orderNumberGuid = Guid.Empty;
            try
            {
                orderNumberGuid = new Guid(orderId);
            }
            catch { }
            Order order = await _orderService.GetOrderByGuid(orderNumberGuid);
            if (order == null)
            {
                return BadRequest();
            }

            var (DetailsSuccess, State, TransactionId, Error) = await processor.GetPaymentDetailsAsync(order.Id);
            var orderAmount = (order.OrderTotal * 100).ToString();
            if (notificationSuccess && DetailsSuccess && orderAmount == amount)
            {
                //order note
                await _orderService.InsertOrderNote(new OrderNote {
                    Note = $"Success: orderId {order.Id}, state {State}, transactionId {TransactionId}",
                    DisplayToCustomer = false,
                    CreatedOnUtc = DateTime.UtcNow,
                    OrderId = order.Id,
                });

                // load settings for a chosen store scope
                // var storeScope = await this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
                // var payturePaymentSettings = _settingService.LoadSetting<PayturePaymentSettings>(storeScope);
                if (State == "Charged")
                {
                    if (await _orderProcessingService.CanMarkOrderAsPaid(order))
                    {
                        order.AuthorizationTransactionId = TransactionId;
                        await _orderService.UpdateOrder(order);
                        await _orderProcessingService.MarkOrderAsPaid(order);
                    }
                }
            }
            else
            {
                if (orderAmount != amount)
                {
                    Error += $" | Order amount {orderAmount} != Payture amount {amount}";
                }
                if (!notificationSuccess)
                {
                    Error += $" | Notification error {errCode}";
                }
                //order note
                await _orderService.InsertOrderNote(new OrderNote {
                    Note = $"Error: {Error}",
                    DisplayToCustomer = false,
                    CreatedOnUtc = DateTime.UtcNow,
                    OrderId = order.Id,
                });
            }

            /*
             * Is3DS=False&ForwardedTo=NotForwarded&Notification=EnginePaySuccess&MerchantContract=Merchant&Success=True&TransactionDate=08.05.2019+15%3A36%3A27&ErrCode=NONE&OrderId=e83f0323-fca0-0f91-9db9-393523563bc5&Amount=12677&MerchantId=1
             * */
            return Ok();
        }
    }
}
