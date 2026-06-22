using Application.Interfaces;
using Infrastructure.Data;
using Infrastructure.Jobs;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services.AddHttpClient("openai");
        services.AddHttpClient("ollama");
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IFinancialHealthService, FinancialHealthService>();
        services.AddScoped<IAIConversationService, AIConversationService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationReminderJob, NotificationReminderJob>();

        return services;
    }
}
