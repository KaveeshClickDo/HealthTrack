using HealthTrack.Controllers;
using HealthTrack.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Areas.Trainer.Controllers
{
    /// <summary>
    /// The base of every Trainer area controller: the area route and the Trainer role check live here only.
    /// </summary>
    [Area("Trainer")]
    [Authorize(Roles = AppRoles.Trainer)]
    public abstract class TrainerAreaController : BaseController
    {
    }
}
