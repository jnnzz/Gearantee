using System.Linq;
using ASI.Basecode.Data.Models;

namespace ASI.Basecode.Data.Interfaces
{
    public interface INotificationRepository
    {
        IQueryable<Notification> Query();
        void Stage(Notification notification);
    }
}
