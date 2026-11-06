using System;
using System.Collections.Generic;
using System.Linq;
using System.Resources;
using System.Security.Cryptography;
using System.Text;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Configuration;
using ASI.Basecode.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using N = ASI.Basecode.Resources.Constants.DomainValues.NotificationTypes;

namespace ASI.Basecode.Services.Services
{
    public class NotificationDispatcher : INotificationDispatcher
    {
        private static readonly ResourceManager Copy = new("ASI.Basecode.Resources.Messages.Notifications", typeof(DomainValues).Assembly);
        private readonly AsiBasecodeDBContext _db;
        private readonly INotificationRepository _repository;
        private readonly NotificationOptions _options;
        private readonly TimeProvider _clock;
        public NotificationDispatcher(IUnitOfWork unitOfWork, INotificationRepository repository,
            IOptions<NotificationOptions> options, TimeProvider clock)
        {
            _db = unitOfWork.Context;
            _repository = repository;
            _options = options.Value;
            _clock = clock;
        }

        public void StageReservation(long reservationId, string type, string detail = null)
        {
            if (!_options.Enabled) return;
            if (_db.Database.CurrentTransaction == null)
                throw new InvalidOperationException("Notifications must be staged inside the workflow transaction.");
            var reservation = _db.Reservations.AsNoTracking().Where(x => x.ReservationId == reservationId)
                .Select(x => new { UserId = x.BorrowerProfile.UserId, x.EquipmentItem.ItemName, x.EquipmentItem.ItemCode }).Single();
            var recipients = new Dictionary<string, string>();
            var staffEvent = type is N.Submitted or N.Cancelled or N.Expired;
            if (!staffEvent) recipients[reservation.UserId] = $"/Reservation/Details/{reservationId}";
            if (staffEvent)
            {
                foreach (var id in Staff(DomainValues.Roles.Custodian, DomainValues.Permissions.ReservationReview))
                    recipients[id] = $"/ReservationReview/Details/{reservationId}";
                // Admins do not have the custodian review route; link to their authorized dashboard.
                foreach (var id in Staff(DomainValues.Roles.Administrator, DomainValues.Permissions.HistoryView))
                    recipients[id] = "/Dashboard/Administrator";
            }
            if (type == N.Overdue)
                foreach (var id in Staff(DomainValues.Roles.Custodian, DomainValues.Permissions.TransactionReleaseReturn))
                    recipients.TryAdd(id, "/ReturnRecord/Index");
            if (type == N.LateReturn)
                foreach (var id in Staff(DomainValues.Roles.Administrator, DomainValues.Permissions.HistoryView))
                    recipients.TryAdd(id, "/BorrowingHistory/Index");

            var activeIds = _db.Users.Where(x => x.IsActive && recipients.Keys.Contains(x.Id)).Select(x => x.Id).ToList();
            var title = Copy.GetString(type) ?? throw new ArgumentException("Unknown notification type", nameof(type));
            var body = string.Format(Copy.GetString("ReservationBody"), reservationId, reservation.ItemName, reservation.ItemCode);
            if (!string.IsNullOrWhiteSpace(detail)) body += " " + detail;
            foreach (var id in activeIds)
            {
                var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{type}:{reservationId}:{id}")));
                if (_repository.Query().Any(x => x.DedupKey == key) || _db.Notifications.Local.Any(x => x.DedupKey == key)) continue;
                var email = id == reservation.UserId && type is N.Approved or N.Rejected;
                _repository.Stage(new Notification
                {
                    UserId = id, Type = type, Title = title, Body = body.Length > 2000 ? body[..2000] : body,
                    LinkUrl = recipients[id], RelatedEntityId = reservationId, DedupKey = key,
                    CreatedAt = _clock.GetUtcNow().UtcDateTime, EmailRequested = email,
                    EmailStatus = email ? "Pending" : null
                });
            }
        }

        private List<string> Staff(string roleName, string permissionName) =>
            (from userRole in _db.UserRoles
             join role in _db.Roles on userRole.RoleId equals role.Id
             join permission in _db.RolePermissions on role.Id equals permission.RoleId
             where role.Name == roleName && permission.Permission.PermissionName == permissionName
             select userRole.UserId).Distinct().ToList();
    }
}
