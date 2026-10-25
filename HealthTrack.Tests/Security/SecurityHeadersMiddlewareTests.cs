using HealthTrack.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;

namespace HealthTrack.Tests.Security;

/// <summary>
/// The security headers of spec 1.9 on every response, with a strict script policy outside Development.
/// </summary>
[TestClass]
public sealed class SecurityHeadersMiddlewareTests
{
    private static async Task<IHeaderDictionary> RunAsync(string environmentName)
    {
        var environment = new HostingEnvironment { EnvironmentName = environmentName, ContentRootFileProvider = new NullFileProvider() };
        var nextRan = false;
        var middleware = new SecurityHeadersMiddleware(_ => { nextRan = true; return Task.CompletedTask; }, environment);
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        Assert.IsTrue(nextRan);
        return context.Response.Headers;
    }

    /// <summary>Every header is set, and in Production scripts and connections are limited to the site itself.</summary>
    [TestMethod]
    public async Task InvokeAsync_Production_AddsHeadersWithStrictPolicy()
    {
        var headers = await RunAsync(Environments.Production);

        Assert.AreEqual(
            "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; " +
            "connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'; object-src 'none'",
            headers.ContentSecurityPolicy.ToString());
        Assert.AreEqual("DENY", headers.XFrameOptions.ToString());
        Assert.AreEqual("nosniff", headers.XContentTypeOptions.ToString());
        Assert.AreEqual("strict-origin-when-cross-origin", headers["Referrer-Policy"].ToString());
        Assert.AreEqual("camera=(), microphone=(), geolocation=()", headers["Permissions-Policy"].ToString());
    }

    /// <summary>In Development only, Browser Link and hot reload on localhost are allowed; inline script never is.</summary>
    [TestMethod]
    public async Task InvokeAsync_Development_AllowsLocalhostToolingOnly()
    {
        var development = (await RunAsync(Environments.Development)).ContentSecurityPolicy.ToString();
        var production = (await RunAsync(Environments.Production)).ContentSecurityPolicy.ToString();

        Assert.Contains("script-src 'self' http://localhost:* https://localhost:*;", development);
        Assert.Contains("ws://localhost:* wss://localhost:*", development);
        Assert.DoesNotContain("localhost", production);
        Assert.DoesNotContain("'unsafe-inline'", development.Split(';').Single(d => d.Trim().StartsWith("script-src", StringComparison.Ordinal)));
    }
}
