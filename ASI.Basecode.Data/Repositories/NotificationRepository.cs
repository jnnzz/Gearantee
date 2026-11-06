using System.Linq;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;

namespace ASI.Basecode.Data.Repositories
{
    public class NotificationRepository : INotificationRepository
    {
        private readonly IUnitOfWork _unitOfWork;
        public NotificationRepository(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;
        public IQueryable<Notification> Query() => _unitOfWork.Context.Notifications;
        // The workflow owns SaveChanges and the transaction.
        public void Stage(Notification notification) => _unitOfWork.Context.Notifications.Add(notification);
    }
}
