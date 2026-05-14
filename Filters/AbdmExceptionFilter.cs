using System;
using System.Net;
using System.Web.Mvc;
using ABDM.Models;
using Newtonsoft.Json;

namespace ABDM.Filters
{
    /// <summary>
    /// MVC global exception filter.
    ///
    /// Catches any unhandled exception from ABDM controllers and:
    ///   1. Logs it via System.Diagnostics.Trace (wire to NLog/Serilog in
    ///      production by replacing the Trace call with your ILogger).
    ///   2. Returns a JSON error envelope (never leaks a stack trace to clients).
    ///   3. Sets an appropriate HTTP status code:
    ///        401 → ABDM auth failure
    ///        503 → ABDM gateway unreachable
    ///        400 → all other ABDM API errors
    ///        500 → unexpected .NET exceptions
    ///
    /// Register in FilterConfig.cs:
    ///   filters.Add(new AbdmExceptionFilter());
    /// </summary>
    public class AbdmExceptionFilter : FilterAttribute, IExceptionFilter
    {
        public void OnException(ExceptionContext context)
        {
            if (context.ExceptionHandled)
                return;

            var ex = context.Exception;

            // ── Determine HTTP status ──────────────────────────────────────────
            int statusCode;

            if (ex is AbdmException abdmEx)
            {
                int apiStatus = abdmEx.ApiError?.HttpStatus ?? 400;

                statusCode = apiStatus switch
                {
                    401 => 401,
                    503 => 503,
                    _   => 400
                };

                LogAbdmError(abdmEx);
            }
            else if (ex is System.Net.Http.HttpRequestException)
            {
                statusCode = 503;   // ABDM unreachable
                LogGenericError("ABDM gateway unreachable", ex);
            }
            else if (ex is TimeoutException || ex is OperationCanceledException)
            {
                statusCode = 504;   // Gateway timeout
                LogGenericError("ABDM request timed out", ex);
            }
            else
            {
                statusCode = 500;
                LogGenericError("Unexpected error", ex);
            }

            // ── Build response ─────────────────────────────────────────────────
            context.HttpContext.Response.StatusCode     = statusCode;
            context.HttpContext.Response.ContentType    = "application/json";
            context.HttpContext.Response.TrySkipIisCustomErrors = true;

            var envelope = new
            {
                success = false,
                error   = new
                {
                    code    = statusCode,
                    message = SafeMessage(ex, statusCode),
                    // Only expose ApiError detail if it exists (no stack traces in prod)
                    detail  = (ex is AbdmException ae) ? ae.ApiError?.Code : null
                }
            };

            context.Result          = new ContentResult
            {
                Content     = JsonConvert.SerializeObject(envelope),
                ContentType = "application/json"
            };
            context.ExceptionHandled = true;
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static string SafeMessage(Exception ex, int statusCode)
        {
            // Never expose internal details to clients
            if (statusCode == 500)
                return "An unexpected error occurred. Please try again later.";

            if (statusCode == 503 || statusCode == 504)
                return "The ABDM gateway is temporarily unavailable. Please retry shortly.";

            // For 400/401 it is safe to surface the ABDM message
            return ex.Message;
        }

        private static void LogAbdmError(AbdmException ex)
        {
            System.Diagnostics.Trace.TraceError(
                "[ABDM] API Error – {0} | HttpStatus: {1} | Code: {2} | RawBody: {3}",
                ex.Message,
                ex.ApiError?.HttpStatus,
                ex.ApiError?.Code,
                ex.ApiError?.RawBody ?? "(none)");
        }

        private static void LogGenericError(string context, Exception ex)
        {
            System.Diagnostics.Trace.TraceError(
                "[ABDM] {0} – {1}\n{2}", context, ex.Message, ex.StackTrace);
        }
    }

    /// <summary>
    /// Optional: logs every inbound request/response for ABDM controllers.
    /// Useful during integration testing; remove or gate behind a config flag in prod.
    ///
    /// Usage: [AbdmRequestLog] on controller class or individual actions.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class AbdmRequestLogAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext ctx)
        {
            System.Diagnostics.Trace.TraceInformation(
                "[ABDM-REQ] {0} {1} | Params: {2}",
                ctx.HttpContext.Request.HttpMethod,
                ctx.HttpContext.Request.RawUrl,
                JsonConvert.SerializeObject(ctx.ActionParameters));
        }

        public override void OnResultExecuted(ResultExecutedContext ctx)
        {
            System.Diagnostics.Trace.TraceInformation(
                "[ABDM-RES] {0} → HTTP {1}",
                ctx.HttpContext.Request.RawUrl,
                ctx.HttpContext.Response.StatusCode);
        }
    }
}
