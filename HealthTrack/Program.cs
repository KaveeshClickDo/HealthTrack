using System.Globalization;
using HealthTrack.Data;
using HealthTrack.Helpers;
using HealthTrack.Models.Entities;
using HealthTrack.Security;
using HealthTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// Services: the database, Data Protection, Identity, MVC and the HealthTrack services (spec 5.3).
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options => options
    .UseSqlServer(connectionString)
    // With MARS on, EF Core can't create savepoints in our transactions and warns on every SaveChanges. We never retry
    // SaveChanges inside a transaction (a failure rolls the whole transaction back), so the savepoints aren't needed.
    .ConfigureWarnings(warnings => warnings.Ignore(SqlServerEventId.SavepointsDisabledBecauseOfMARS)));

// Data Protection keys encrypt the health fields (BR-20) and the auth cookie; they live in App_Data/keys so both survive restarts.
var dataProtection = builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys")))
    .SetApplicationName("HealthTrack");
if (OperatingSystem.IsWindows())
{
    dataProtection.ProtectKeysWithDpapi();
}

// ASP.NET Core Identity with our own MVC Account pages (no Identity UI package).
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        // BR-01: 8+ characters with upper case, lower case, a digit and a symbol; the email is the unique user name.
        options.Password.RequiredLength = 8;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireDigit = true;
        options.Password.RequireNonAlphanumeric = true;
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;

        // Spec 1.9: 5 failed attempts lock the account for 15 minutes, for every user.
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders()
    .AddClaimsPrincipalFactory<HealthTrackClaimsPrincipalFactory>();

// PBKDF2 with HMAC-SHA512 and a random salt; 100,000 iterations is the .NET default, stated explicitly.
builder.Services.Configure<PasswordHasherOptions>(options => options.IterationCount = 100_000);

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Error/Forbidden";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;

    // The bell polls every minute. That background traffic must not count as activity, or an open tab would keep the
    // session alive for ever and the 30-minute idle time-out would never apply. Both renewals are switched off for it:
    // sliding expiration, and the refresh after the security-stamp check (which still runs and still signs out).
    options.Events.OnCheckSlidingExpiration = context =>
    {
        if (IsNotificationPoll(context.Request))
        {
            context.ShouldRenew = false;
        }

        return Task.CompletedTask;
    };
    var validatePrincipal = options.Events.OnValidatePrincipal;
    options.Events.OnValidatePrincipal = async context =>
    {
        await validatePrincipal(context);
        if (IsNotificationPoll(context.Request))
        {
            context.ShouldRenew = false;
        }
    };
});

// BR-05: a changed security stamp (suspension, password change) signs old cookies out within a minute.
builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.FromMinutes(1));

// Every page needs a signed-in user unless it says [AllowAnonymous].
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// Every POST, including AJAX (token in the RequestVerificationToken header), is checked for an anti-forgery token.
builder.Services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");
builder.Services.AddHttpContextAccessor();

builder.Services.AddSingleton<IClock, SystemClock>();
// Singleton: EF Core caches the model, so the encryption converter keeps the first instance it is given.
builder.Services.AddSingleton<IEncryptionService, EncryptionService>();

// Email copies of notifications (SMTP, or .eml files in App_Data/mail) and the reminder timer (spec 1.8).
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
builder.Services.Configure<ReminderOptions>(builder.Configuration.GetSection(ReminderOptions.SectionName));

builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IReminderService, ReminderService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IAssignmentService, AssignmentService>();
builder.Services.AddScoped<ILibraryService, LibraryService>();
builder.Services.AddScoped<IWorkoutService, WorkoutService>();
builder.Services.AddScoped<IFeedbackService, FeedbackService>();
builder.Services.AddScoped<IProgressService, ProgressService>();
builder.Services.AddScoped<IDietService, DietService>();
builder.Services.AddScoped<IAppointmentService, AppointmentService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<INavigationService, NavigationService>();

// UC34: creates due reminders for everyone every minute, even when nobody is signed in.
builder.Services.AddHostedService<ReminderBackgroundService>();

var app = builder.Build();

// Apply migrations and seed before the first request, so a fresh clone only needs F5.
await DbSeeder.SeedAsync(app.Services);

// The request pipeline, in the order of CLAUDE.md: errors, HSTS, HTTPS, security headers, routing, sign-in, endpoints.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error/ServerError");
    // HSTS (30 days, the default) tells browsers to use HTTPS only; never sent to localhost, so it is off in Development.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Error/{0}");
app.UseHttpsRedirection();

// Spec 1.9: Content-Security-Policy, X-Frame-Options, X-Content-Type-Options, Referrer-Policy and Permissions-Policy.
app.UseMiddleware<SecurityHeadersMiddleware>();

// One culture for everyone, so decimals use a dot and dates read the same on every machine (dd MMM yyyy).
var appCulture = new CultureInfo("en-GB");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(appCulture),
    SupportedCultures = [appCulture],
    SupportedUICultures = [appCulture]
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Static files are endpoints too, so they must opt out of the fallback policy or the login page loses its CSS and JS.
app.MapStaticAssets().AllowAnonymous();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

// The bell's background poll (NotificationsController.Poll).
static bool IsNotificationPoll(HttpRequest request) =>
    request.Path.StartsWithSegments("/Notifications/Poll", StringComparison.OrdinalIgnoreCase);
