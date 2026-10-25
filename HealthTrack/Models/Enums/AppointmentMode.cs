using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.Enums
{
    /// <summary>
    /// Where an appointment takes place.
    /// </summary>
    public enum AppointmentMode
    {
        /// <summary>Face to face; Location holds the room.</summary>
        [Display(Name = "In person")]
        InPerson = 1,

        /// <summary>A video call; Location holds the meeting link.</summary>
        [Display(Name = "Online")]
        Online = 2
    }
}
