using Grand.Framework.Mvc.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Grand.Plugin.Payments.Payture
{
    public partial class RouteProvider : IRouteProvider
    {
        public void RegisterRoutes(IEndpointRouteBuilder routeBuilder)
        {
            //PDT
            routeBuilder.MapControllerRoute("Plugin.Payments.Payture.ReturnUrlHandler",
                 "Plugins/PaymentPayture/ReturnUrlHandler",
                 new { controller = "PaymentPayture", action = "ReturnUrlHandler" }
            );
            //IPN
            routeBuilder.MapControllerRoute("Plugin.Payments.Payture.NotificationHandler",
                 "Plugins/PaymentPayture/NotificationHandler",
                 new { controller = "PaymentPayture", action = "NotificationHandler" }
            );
        }
        public int Priority
        {
            get
            {
                return 0;
            }
        }
    }
}
