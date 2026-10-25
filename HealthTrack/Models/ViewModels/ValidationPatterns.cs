namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// Regular expressions shared by the forms. They avoid \p{L} and other .NET-only syntax, so jQuery Validation runs
    /// the same pattern in the browser as data annotations run on the server.
    /// </summary>
    public static class ValidationPatterns
    {
        /// <summary>Letters (including accented Latin letters) with single spaces or hyphens between words.</summary>
        public const string PersonName = @"^[A-Za-zÀ-ÖØ-öø-ÿ]+(?:[ -][A-Za-zÀ-ÖØ-öø-ÿ]+)*$";

        /// <summary>Message for <see cref="PersonName"/>.</summary>
        public const string PersonNameMessage = "Use letters, spaces and hyphens only.";

        /// <summary>A phone number: an optional +, then digits, spaces, brackets and hyphens.</summary>
        public const string Phone = @"^\+?[0-9][0-9 ()-]{6,18}$";

        /// <summary>Message for <see cref="Phone"/>.</summary>
        public const string PhoneMessage = "Enter a valid phone number, for example +94 77 123 4567.";

        /// <summary>BR-01: at least 8 characters with upper case, lower case, a digit and a symbol.</summary>
        public const string Password = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,}$";

        /// <summary>Message for <see cref="Password"/>.</summary>
        public const string PasswordMessage = "Use at least 8 characters with upper case, lower case, a digit and a symbol.";
    }
}
