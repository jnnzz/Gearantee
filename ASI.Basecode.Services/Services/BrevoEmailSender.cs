using System.Net;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ASI.Basecode.Services.Services
{
    public sealed class BrevoEmailSender : IBrevoEmailSender
    {
        private readonly HttpClient _httpClient;
        private readonly BrevoOptions _options;
        private readonly ILogger<BrevoEmailSender> _logger;

        public BrevoEmailSender(
            HttpClient httpClient,
            IOptions<BrevoOptions> options,
            ILogger<BrevoEmailSender> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _logger = logger;
        }

        public async Task SendPasswordResetOtpAsync(
            string recipientEmail,
            string recipientName,
            string otp,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                throw new InvalidOperationException(
                    "Brevo:ApiKey is not configured.");
            }

            if (string.IsNullOrWhiteSpace(_options.SenderEmail))
            {
                throw new InvalidOperationException(
                    "Brevo:SenderEmail is not configured.");
            }

            var safeRecipientName = WebUtility.HtmlEncode(recipientName);
            var htmlContent = $@"
<!doctype html>
<html lang=""en"">
<body style=""margin:0;background:#f2f2f3;color:#1b2f40;font-family:Arial,sans-serif;line-height:1.6;"">
    <div style=""max-width:560px;margin:40px auto;padding:32px;background:#ffffff;"">
        <h1 style=""margin-top:0;color:#1b2f40;"">Your Gearantee verification code</h1>
        <p>Hello {safeRecipientName},</p>
        <p>Use this six-digit code to continue resetting your Gearantee password:</p>
        <p style=""margin:24px 0;font-size:32px;font-weight:700;letter-spacing:8px;color:#5980a6;"">{otp}</p>
        <p>This code expires in 10 minutes and can be used once.</p>
        <p>If you did not request this, you can safely ignore this email.</p>
    </div>
</body>
</html>";

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "v3/smtp/email")
            {
                Content = JsonContent.Create(new
                {
                    sender = new
                    {
                        email = _options.SenderEmail,
                        name = _options.SenderName
                    },
                    to = new[]
                    {
                        new
                        {
                            email = recipientEmail,
                            name = recipientName
                        }
                    },
                    subject = "Your Gearantee password reset code",
                    htmlContent
                })
            };

            request.Headers.TryAddWithoutValidation("api-key", _options.ApiKey);
            request.Headers.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _httpClient.SendAsync(
                request,
                cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            _logger.LogError(
                "Brevo password-reset email failed with status {StatusCode}.",
                (int)response.StatusCode);

            throw new HttpRequestException(
                $"Brevo email request failed with status code {(int)response.StatusCode}.");
        }
    }
}
