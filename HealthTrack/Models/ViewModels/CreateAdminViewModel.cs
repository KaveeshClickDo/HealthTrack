namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The form an administrator uses to create another administrator (BR-04: admins never self-register).
    /// It has the same fields and rules as registration; the consent box confirms the new admin accepted the privacy notice.
    /// </summary>
    public class CreateAdminViewModel : RegisterViewModel
    {
    }
}
