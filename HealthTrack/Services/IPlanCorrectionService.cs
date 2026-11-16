namespace HealthTrack.Services
{
    /// <summary>
    /// The administrator's controlled correction of a plan assigned to the wrong client (EXT-02, BR-24). The
    /// administrator only moves the plan; its contents and activation stay with the professional.
    /// </summary>
    public interface IPlanCorrectionService
    {
        /// <summary>
        /// Moves a workout or diet plan to the client it was meant for. Rules (BR-24): the plan is a draft or active client
        /// plan; nothing has been logged against it; the new client is another client actively assigned to the plan's
        /// professional; a reason is given. The moved plan becomes a draft, so the professional checks it and activates it
        /// for the right client. The professional and the client who had it are notified, and the move is audited with the
        /// reason.
        /// </summary>
        /// <param name="isDietPlan">Whether the plan is a diet plan; otherwise a workout plan.</param>
        /// <param name="planId">The plan.</param>
        /// <param name="newClientId">The client it should belong to.</param>
        /// <param name="reason">Why it is being moved.</param>
        /// <param name="adminUserId">The administrator making the correction.</param>
        /// <returns>The user ID of the client who had the plan, for the redirect; null when the plan doesn't exist.</returns>
        /// <exception cref="Helpers.BusinessRuleException">BR-24: any of the rules above is broken.</exception>
        Task<string?> MovePlanAsync(bool isDietPlan, int planId, int newClientId, string reason, string adminUserId);
    }
}
