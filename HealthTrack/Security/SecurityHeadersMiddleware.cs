namespace HealthTrack.Security
{
    /// <summary>
    /// Adds the security headers of spec 1.9 to every response, pages, files and error pages alike: a Content-Security-Policy
    /// that allows only the site's own scripts (so injected markup can't run script), no framing (clickjacking), no MIME
    /// sniffing, a strict referrer policy, and no camera, microphone or location. Registered after HTTPS redirection.
    /// </summary>
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string _contentSecurityPolicy;

        /// <summary>
        /// Creates the middleware.
        /// </summary>
        /// <param name="next">The rest of the pipeline.</param>
        /// <param name="environment">In Development the policy also allows Visual Studio's Browser Link and hot reload.</param>
        public SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment environment)
        {
            _next = next;
            _contentSecurityPolicy = ContentSecurityPolicy(environment.IsDevelopment());
        }

        /// <summary>
        /// Sets the headers, then runs the rest of the pipeline. An error page re-executed by the exception handler passes
        /// through here again, so it gets the headers too.
        /// </summary>
        /// <param name="context">The request.</param>
        /// <returns>A task that completes when the response is done.</returns>
        public Task InvokeAsync(HttpContext context)
        {
            var headers = context.Response.Headers;
            headers.ContentSecurityPolicy = _contentSecurityPolicy;
            headers.XFrameOptions = "DENY";
            headers.XContentTypeOptions = "nosniff";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            return _next(context);
        }

        /// <summary>
        /// The Content-Security-Policy. Scripts come only from the site itself, so the views keep their scripts in wwwroot/js
        /// and never use inline script or onclick attributes. Inline styles are allowed (Bootstrap and Chart.js set some).
        /// </summary>
        /// <param name="development">
        /// Whether to also allow scripts from, and connections to, localhost on any port: Visual Studio's Browser Link and
        /// hot reload inject a script from their own port and talk to it over WebSockets (or plain HTTP as a fallback).
        /// </param>
        /// <returns>The header value.</returns>
        public static string ContentSecurityPolicy(bool development)
        {
            var scripts = development ? "'self' http://localhost:* https://localhost:*" : "'self'";
            var connections = development ? "'self' ws://localhost:* wss://localhost:* http://localhost:* https://localhost:*" : "'self'";
            return "default-src 'self'; "
                + $"script-src {scripts}; "
                + "style-src 'self' 'unsafe-inline'; "
                + "img-src 'self' data:; "
                + "font-src 'self'; "
                + $"connect-src {connections}; "
                + "frame-ancestors 'none'; "
                + "base-uri 'self'; "
                + "form-action 'self'; "
                + "object-src 'none'";
        }
    }
}
