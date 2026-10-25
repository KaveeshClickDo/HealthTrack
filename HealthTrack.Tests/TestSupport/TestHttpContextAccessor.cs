using Microsoft.AspNetCore.Http;

namespace HealthTrack.Tests.TestSupport;

/// <summary>
/// An <see cref="IHttpContextAccessor"/> that simply holds one context. ASP.NET Core's own accessor stores it in a static
/// AsyncLocal, which neither flows out of the async method that sets it nor stays separate between parallel tests.
/// </summary>
public sealed class TestHttpContextAccessor : IHttpContextAccessor
{
    /// <inheritdoc />
    public HttpContext? HttpContext { get; set; }
}
