using HealthTrack.Data;
using HealthTrack.Helpers;
using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;
using HealthTrack.Security;
using HealthTrack.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace HealthTrack.Tests.TestSupport;

/// <summary>
/// The app's services for one test, on a fresh EF Core InMemory database: Identity (stores, roles and sign-in with a cookie
/// scheme on a fake HTTP request), the HealthTrack services, a fixed clock and the fake encryption service.
/// Identity uses the same password and lockout options as Program.cs.
/// </summary>
public sealed class TestHost : IAsyncDisposable
{
    /// <summary>The default time for tests: Sunday 25 October 2026, 09:00.</summary>
    public static readonly DateTime DefaultNow = new(2026, 10, 25, 9, 0, 0);

    /// <summary>The password every user created by <see cref="CreateUserAsync"/> gets.</summary>
    public const string Password = "Passw0rd!";

    private readonly ServiceProvider _root;
    private readonly AsyncServiceScope _scope;

    private TestHost(ServiceProvider root, FixedClock clock, TestHttpContextAccessor httpContextAccessor)
    {
        _root = root;
        _scope = root.CreateAsyncScope();
        Clock = clock;

        // SignInManager and AuditService read the current request; this one carries the test's scoped services.
        httpContextAccessor.HttpContext = new DefaultHttpContext { RequestServices = _scope.ServiceProvider };
    }

    /// <summary>The test's clock.</summary>
    public FixedClock Clock { get; }

    /// <summary>The test's scoped services (one DbContext for the whole test, as in one web request).</summary>
    public IServiceProvider Services => _scope.ServiceProvider;

    /// <summary>The test's database context.</summary>
    public ApplicationDbContext Db => Get<ApplicationDbContext>();

    /// <summary>
    /// Builds the services, creates the three roles and returns the host.
    /// </summary>
    /// <param name="now">The clock's time; <see cref="DefaultNow"/> when omitted.</param>
    /// <returns>The host; dispose it at the end of the test.</returns>
    public static async Task<TestHost> CreateAsync(DateTime? now = null)
    {
        var clock = new FixedClock(now ?? DefaultNow);
        var httpContextAccessor = new TestHttpContextAccessor();
        var databaseName = Guid.NewGuid().ToString();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHttpContextAccessor>(httpContextAccessor);
        services.AddSingleton<IClock>(clock);
        services.AddSingleton<IEncryptionService, FakeEncryptionService>();
        services.AddDbContext<ApplicationDbContext>(options => options
            .UseInMemoryDatabase(databaseName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddCookie(IdentityConstants.ApplicationScheme);
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireDigit = true;
                options.Password.RequireNonAlphanumeric = true;
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedAccount = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        // The production 100,000 iterations would only slow the tests down; hashing itself isn't under test.
        services.Configure<PasswordHasherOptions>(options => options.IterationCount = 1_000);

        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IAssignmentService, AssignmentService>();

        var host = new TestHost(services.BuildServiceProvider(), clock, httpContextAccessor);

        var roleManager = host.Get<RoleManager<IdentityRole>>();
        foreach (var role in AppRoles.All)
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }

        return host;
    }

    /// <summary>
    /// Resolves a service from the test's scope.
    /// </summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <returns>The service.</returns>
    public T Get<T>() where T : notnull => Services.GetRequiredService<T>();

    /// <summary>
    /// Creates a user with <see cref="Password"/> in one role through UserManager, as registration does.
    /// </summary>
    /// <param name="email">Email address and user name.</param>
    /// <param name="role">The role.</param>
    /// <param name="status">The account status.</param>
    /// <param name="firstName">First name.</param>
    /// <param name="lastName">Last name.</param>
    /// <returns>The saved user.</returns>
    public async Task<ApplicationUser> CreateUserAsync(string email, string role, AccountStatus status = AccountStatus.Active,
        string firstName = "Test", string lastName = "User")
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            DateOfBirth = new DateTime(1990, 1, 1),
            Gender = Gender.Other,
            AccountStatus = status,
            PrivacyConsentAt = Clock.Now,
            CreatedAt = Clock.Now
        };

        var userManager = Get<UserManager<ApplicationUser>>();
        var result = await userManager.CreateAsync(user, Password);
        if (result.Succeeded)
        {
            result = await userManager.AddToRoleAsync(user, role);
        }

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        return user;
    }

    /// <summary>
    /// Creates a client account with a profile.
    /// </summary>
    /// <param name="email">Email address.</param>
    /// <param name="firstName">First name, for ordering checks.</param>
    /// <param name="status">The account status.</param>
    /// <returns>The saved profile, with its user.</returns>
    public async Task<ClientProfile> CreateClientAsync(string email, string firstName = "Client", AccountStatus status = AccountStatus.Active)
    {
        var user = await CreateUserAsync(email, AppRoles.Client, status, firstName);
        var profile = new ClientProfile
        {
            UserId = user.Id,
            User = user,
            FitnessGoal = FitnessGoal.LoseWeight,
            ActivityLevel = ActivityLevel.ModeratelyActive,
            CreatedAt = Clock.Now
        };

        Db.ClientProfiles.Add(profile);
        await Db.SaveChangesAsync();
        return profile;
    }

    /// <summary>
    /// Creates an approved, active fitness trainer with a profile.
    /// </summary>
    /// <param name="email">Email address.</param>
    /// <returns>The saved profile, with its user.</returns>
    public async Task<TrainerProfile> CreateTrainerAsync(string email)
    {
        var user = await CreateUserAsync(email, AppRoles.Trainer);
        var profile = new TrainerProfile
        {
            UserId = user.Id,
            User = user,
            ProfessionalType = ProfessionalType.FitnessTrainer,
            Specialization = "Strength",
            Qualifications = "Level 3",
            ApprovedAt = Clock.Now,
            CreatedAt = Clock.Now
        };

        Db.TrainerProfiles.Add(profile);
        await Db.SaveChangesAsync();
        return profile;
    }

    /// <summary>
    /// Adds an assignment between a trainer and a client.
    /// </summary>
    /// <param name="trainer">The trainer.</param>
    /// <param name="client">The client.</param>
    /// <param name="endDate">The end date; null for an active assignment.</param>
    /// <returns>The saved assignment.</returns>
    public async Task<TrainerAssignment> AssignAsync(TrainerProfile trainer, ClientProfile client, DateTime? endDate = null)
    {
        var assignment = new TrainerAssignment
        {
            TrainerId = trainer.TrainerId,
            ClientId = client.ClientId,
            StartDate = Clock.Today.AddMonths(-3),
            EndDate = endDate
        };

        Db.TrainerAssignments.Add(assignment);
        await Db.SaveChangesAsync();
        return assignment;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _root.DisposeAsync();
    }
}
