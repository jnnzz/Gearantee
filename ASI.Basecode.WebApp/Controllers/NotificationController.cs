using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Services.Configuration;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.WebApp.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ASI.Basecode.WebApp.Controllers
{
    [Authorize]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class NotificationController : ControllerBase<NotificationController>
    {
        private readonly INotificationService _notifications;
        private readonly NotificationOptions _options;
        public NotificationController(INotificationService notifications, IOptions<NotificationOptions> options,
            IHttpContextAccessor accessor, ILoggerFactory loggerFactory, IConfiguration configuration)
            : base(accessor, loggerFactory, configuration)
        { _notifications = notifications; _options = options.Value; }

        [HttpGet]
        public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
        {
            if (!_options.Enabled) return NotFound();
            return View(await _notifications.GetPageAsync(UserId, page, cancellationToken));
        }

        [HttpGet]
        public async Task<IActionResult> Recent(CancellationToken cancellationToken)
        {
            if (!_options.Enabled) return NotFound();
            return Json((await _notifications.GetPageAsync(UserId, 1, cancellationToken)).Items);
        }

        [HttpGet]
        public async Task<IActionResult> UnreadCount(CancellationToken cancellationToken)
        {
            if (!_options.Enabled) return NotFound();
            return Json(new { count = await _notifications.UnreadCountAsync(UserId, cancellationToken) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkRead(long id, int page = 1, CancellationToken cancellationToken = default)
        {
            if (!_options.Enabled) return NotFound();
            await _notifications.MarkReadAsync(UserId, id, cancellationToken);
            return RedirectToAction(nameof(Index), new { page });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
        {
            if (!_options.Enabled) return NotFound();
            await _notifications.MarkReadAsync(UserId, null, cancellationToken);
            TempData["SuccessMessage"] = "All notifications marked as read.";
            return RedirectToAction(nameof(Index));
        }
    }
}
