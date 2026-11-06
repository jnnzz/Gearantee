namespace ASI.Basecode.Services.Interfaces
{
    /// <summary>
    /// Centralized administrative audit logging. Staged events commit atomically with the
    /// change they describe through the caller's unit of work.
    /// </summary>
    public interface IAuditService
    {
        void Stage(
            string actorUserId,
            string targetUserId,
            string targetRoleId,
            string action,
            object details);

        void Stage(
            string actorUserId,
            string targetUserId,
            string action,
            object details);
    }
}
