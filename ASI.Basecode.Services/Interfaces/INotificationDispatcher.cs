namespace ASI.Basecode.Services.Interfaces
{
    public interface INotificationDispatcher
    {
        void StageReservation(long reservationId, string type, string detail = null);
    }
}
