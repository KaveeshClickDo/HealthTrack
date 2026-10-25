using HealthTrack.Controllers;
using HealthTrack.Security;
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
    }
}
