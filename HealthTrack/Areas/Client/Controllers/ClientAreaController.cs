using HealthTrack.Controllers;
using HealthTrack.Security;
using HealthTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Areas.Client.Controllers
{
    /// <summary>
    /// The base of every Client area controller: the area route and the Client role check live here only.
    /// </summary>
    [Area("Client")]
    [Authorize(Roles = AppRoles.Client)]
    public abstract class ClientAreaController : BaseController
    {
        /// <summary>
        /// The signed-in client's profile key. "My" data is always found from the signed-in user, never from a form or URL.
        /// </summary>
        /// <returns>The ClientId, or null when the user has no client profile.</returns>
        protected Task<int?> CurrentClientIdAsync() =>
            HttpContext.RequestServices.GetRequiredService<IAccountService>().GetClientIdAsync(CurrentUserId);
    }
}
