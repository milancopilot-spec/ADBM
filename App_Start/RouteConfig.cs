using System.Web.Mvc;
using System.Web.Routing;

namespace ABDM
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
            routes.MapMvcAttributeRoutes();
            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "M1HealthId", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}
