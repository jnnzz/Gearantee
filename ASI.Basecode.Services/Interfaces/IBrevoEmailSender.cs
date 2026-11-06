using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IBrevoEmailSender
    {
        Task SendAsync(string recipientEmail, string recipientName, string subject, string htmlContent, CancellationToken cancellationToken = default);

        Task SendPasswordResetOtpAsync(
            string recipientEmail,
            string recipientName,
            string otp,
            CancellationToken cancellationToken = default);
    }
}
