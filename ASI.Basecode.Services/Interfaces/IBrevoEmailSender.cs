using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IBrevoEmailSender
    {
        Task SendPasswordResetOtpAsync(
            string recipientEmail,
            string recipientName,
            string otp,
            CancellationToken cancellationToken = default);
    }
}
