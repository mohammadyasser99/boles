using CarRental.Application.Interfaces;
using CarRental.Application.Services;
using CarRental.Domain.Interfaces;
using CarRental.Infrastructure.Persistence;
using CarRental.Infrastructure.Repositories;
using CarRental.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CarRental.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── EF Core / SQL Server ─────────────────────────────────────────────
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));

            options.UseLazyLoadingProxies();
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        // ── Repositories ─────────────────────────────────────────────────────
        services.AddScoped<IAdminRepository, AdminRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICarRepository, CarRepository>();
        services.AddScoped<IFineRepository, FineRepository>();
services.AddScoped<IMailManager, MailService > ();
        // ── Infrastructure Services ──────────────────────────────────────────
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IExcelParserService, ExcelParserService>();

        // ── Application Services ─────────────────────────────────────────────
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IFineService, FineService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ICarService, CarService>();
        services.AddScoped<IAdminService, AdminService>();

        services.AddScoped<IEntranceFeeRepository, EntranceFeeRepository>();
        services.AddScoped<IExcelEntranceFeeParserService, ExcelEntranceFeeParserService>();
        services.AddScoped<IEntranceFeeService, EntranceFeeService>();

        services.AddScoped<IDebtCalculatorService, DebtCalculatorService>();

        services.AddScoped<IUserDocumentRepository, UserDocumentRepository>();
        services.AddScoped<IFileStorageService, FileStorageService>();
        services.AddScoped<IUserDocumentService, UserDocumentService>();

        services.AddScoped<IWhatsAppSenderService, TwilioWhatsAppSenderService>();
        services.AddScoped<IWhatsAppService, WhatsAppService>();
        services.AddScoped<IMonthlyRentalPaymentService , MonthlyRentalPaymentService>();
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));


        return services;
    }
}
