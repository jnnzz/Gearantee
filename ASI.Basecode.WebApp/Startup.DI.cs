
using ASI.Basecode.Data;
// OTP store moved to Data
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using System;

namespace ASI.Basecode.WebApp
{
    internal partial class StartupConfigurer
    {
        private void ConfigureOtherServices()
        {
            _services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            _services.TryAddSingleton<IActionContextAccessor, ActionContextAccessor>();
            _services.TryAddSingleton<TimeProvider>(TimeProvider.System);
            _services.AddScoped<IUnitOfWork, UnitOfWork>();
            _services.AddScoped<IAccountService, AccountService>();
            _services.AddScoped<IAuditService, AuditService>();

            _services.Configure<BrevoOptions>(
                Configuration.GetSection(BrevoOptions.SectionName));

            _services.AddHttpClient<IBrevoEmailSender, BrevoEmailSender>(
                (serviceProvider, client) =>
                {
                    var options = serviceProvider
                        .GetRequiredService<IOptions<BrevoOptions>>()
                        .Value;

                    client.BaseAddress = new Uri(
                        options.BaseUrl.TrimEnd('/') + "/");
                });

            _services.AddScoped<IPasswordResetOtpStore, IdentityPasswordResetOtpStore>();
            _services.AddScoped<IPasswordResetOtpService, PasswordResetOtpService>();
            _services.AddScoped<IDashboardService, DashboardService>();
            _services.AddScoped<IUserAdministrationService, UserAdministrationService>();

            // WBS 30.00â€“32.00: Borrower Profile Management
            _services.AddScoped<IBorrowerProfileService, BorrowerProfileService>();

            _services.AddScoped<ICategoryService, CategoryService>();
            _services.AddScoped<IEquipmentItemService, EquipmentItemService>();
            _services.AddScoped<IAvailabilityCalendarService, AvailabilityCalendarService>();

            // Repositories
            _services.AddScoped<IEquipmentItemRepository, EquipmentItemRepository>();
            _services.AddScoped<IBorrowerProfileRepository, BorrowerProfileRepository>();
            _services.AddScoped<IReservationRepository, ReservationRepository>();
            _services.AddScoped<IReturnRecordRepository, ReturnRecordRepository>();

            // Services
            _services.AddScoped<ICatalogService, CatalogService>();
            _services.AddScoped<IReservationService, ReservationService>();
            _services.AddScoped<IReturnRecordService, ReturnRecordService>();
            _services.AddScoped<IBorrowingHistoryService, BorrowingHistoryService>();
        }
    }
}
