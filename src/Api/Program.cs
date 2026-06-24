using System.Text;
using System.Text.Json.Serialization;
using Api.Services;
using Application.Interfaces;
using Hangfire;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", true);

var builder = WebApplication.CreateBuilder(args);

ValidateProductionConfiguration(builder.Configuration, builder.Environment);

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
        else
        {
            var frontendUrl = builder.Configuration["Frontend:Url"]!;

            policy.WithOrigins(frontendUrl)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

var jwtKey = builder.Configuration["Jwt:Key"] ?? "development-only-secret-key-change-me";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddHangfire(config =>
    config.UseSqlServerStorage(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHangfireServer();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Frontend");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseHangfireDashboard("/hangfire");
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new { status = "ok", service = "FamilyBudgetAI.Api" }));
app.MapControllers();

RecurringJob.AddOrUpdate<INotificationReminderJob>(
    "renewal-reminders",
    job => job.CreateRenewalRemindersAsync(),
    Cron.Daily);

app.Run();

static void ValidateProductionConfiguration(IConfiguration configuration, IWebHostEnvironment environment)
{
    if (environment.IsDevelopment())
    {
        return;
    }

    var requiredKeys = new[]
    {
        "ConnectionStrings:DefaultConnection",
        "Jwt:Key",
        "Jwt:Issuer",
        "Jwt:Audience",
        "Frontend:Url"
    };

    var missingKeys = requiredKeys
        .Where(key => string.IsNullOrWhiteSpace(configuration[key]))
        .ToArray();

    if (missingKeys.Length > 0)
    {
        throw new InvalidOperationException(
            "Missing required production configuration: " + string.Join(", ", missingKeys));
    }

    var jwtKey = configuration["Jwt:Key"]!;
    if (jwtKey.Contains("development-only", StringComparison.OrdinalIgnoreCase) || jwtKey.Length < 32)
    {
        throw new InvalidOperationException("Jwt:Key must be a production secret with at least 32 characters.");
    }

    var frontendUrl = configuration["Frontend:Url"]!;
    if (!Uri.TryCreate(frontendUrl, UriKind.Absolute, out _))
    {
        throw new InvalidOperationException("Frontend:Url must be an absolute URL, for example https://your-frontend.vercel.app.");
    }
}

public partial class Program;
