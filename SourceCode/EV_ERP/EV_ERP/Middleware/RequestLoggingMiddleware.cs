using System.Diagnostics;

namespace EV_ERP.Middleware
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var sw = Stopwatch.StartNew();
            var path = context.Request.Path;
            var method = context.Request.Method;

            try
            {
                await _next(context);
                sw.Stop();

                var statusCode = context.Response.StatusCode;
                if (sw.ElapsedMilliseconds > 500)
                {
                    _logger.LogWarning("SLOW REQUEST: {Method} {Path} → {StatusCode} in {Elapsed}ms",
                        method, path, statusCode, sw.ElapsedMilliseconds);
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "UNHANDLED: {Method} {Path} after {Elapsed}ms", method, path, sw.ElapsedMilliseconds);
                throw;
            }
        }
    }
}
