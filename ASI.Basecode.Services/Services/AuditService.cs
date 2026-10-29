using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using System;
using System.Text.Json;

namespace ASI.Basecode.Services.Services
{
    /// <summary>
    /// Central staging point for administrative audit events. Events are attached to the
    /// caller's ambient DbContext so the audit record and the change it describes commit
    /// atomically in the caller's transaction.
    /// </summary>
    public class AuditService : IAuditService
    {
        private readonly AsiBasecodeDBContext _db;

        public AuditService(IUnitOfWork unitOfWork)
        {
            _db = unitOfWork.Context;
        }

        /// <inheritdoc />
        public void Stage(
            string actorUserId,
            string targetUserId,
            string targetRoleId,
            string action,
            object details)
        {
            _db.AdministrationAuditEvents.Add(new AdministrationAuditEvent
            {
                ActorUserId = actorUserId,
                TargetUserId = targetUserId,
                TargetRoleId = targetRoleId,
                Action = action,
                DetailsJson = JsonSerializer.Serialize(details),
                OccurredAt = DateTime.UtcNow
            });
        }

        /// <inheritdoc />
        public void Stage(
            string actorUserId,
            string targetUserId,
            string action,
            object details)
        {
            Stage(actorUserId, targetUserId, null, action, details);
        }
    }
}
