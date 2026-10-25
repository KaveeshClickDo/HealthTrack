using HealthTrack.Controllers;
using HealthTrack.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Areas.Admin.Controllers
{
    /// <summary>
    /// The base of every Admin area controller: the area route and the Admin role check live here only.
    /// </summary>
    [Area("Admin")]
    [Authorize(Roles = AppRoles.Admin)]
    public abstract class AdminAreaController : BaseController
    {
    }
}
