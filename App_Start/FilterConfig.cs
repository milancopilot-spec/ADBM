// ── FilterConfig.cs ──────────────────────────────────────────────────────────
using System.Web.Mvc;
using ABDM.Filters;

namespace ABDM.App_Start
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());   // standard MVC error page
            filters.Add(new AbdmExceptionFilter());    // JSON error envelope for API calls
        }
    }
}
