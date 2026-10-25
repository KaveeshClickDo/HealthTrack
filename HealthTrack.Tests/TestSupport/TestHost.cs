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
        services.AddScoped<ILibraryService, LibraryService>();
        services.AddScoped<IWorkoutService, WorkoutService>();
        services.AddScoped<IFeedbackService, FeedbackService>();
        services.AddScoped<IProgressService, ProgressService>();
        services.AddScoped<IDietService, DietService>();

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
    /// Creates a fitness trainer with a profile; approved and active unless told otherwise.
    /// </summary>
    /// <param name="email">Email address.</param>
    /// <param name="approved">Whether ApprovedAt is set; an unapproved trainer is PendingApproval unless a status is given.</param>
    /// <param name="status">The account status; Active for approved trainers by default.</param>
    /// <param name="maxClients">The trainer's capacity.</param>
    /// <param name="firstName">First name.</param>
    /// <returns>The saved profile, with its user.</returns>
    public async Task<TrainerProfile> CreateTrainerAsync(string email, bool approved = true, AccountStatus? status = null,
        int maxClients = 20, string firstName = "Trainer")
    {
        var user = await CreateUserAsync(email, AppRoles.Trainer,
            status ?? (approved ? AccountStatus.Active : AccountStatus.PendingApproval), firstName);
        var profile = new TrainerProfile
        {
            UserId = user.Id,
            User = user,
            ProfessionalType = ProfessionalType.FitnessTrainer,
            Specialization = "Strength",
            Qualifications = "Level 3",
            MaxClients = maxClients,
            ApprovedAt = approved ? Clock.Now : null,
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
    /// <param name="startDate">The start date; three months ago by default.</param>
    /// <returns>The saved assignment.</returns>
    public async Task<TrainerAssignment> AssignAsync(TrainerProfile trainer, ClientProfile client, DateTime? endDate = null,
        DateTime? startDate = null)
    {
        var assignment = new TrainerAssignment
        {
            TrainerId = trainer.TrainerId,
            ClientId = client.ClientId,
            StartDate = startDate ?? Clock.Today.AddMonths(-3),
            EndDate = endDate
        };

        Db.TrainerAssignments.Add(assignment);
        await Db.SaveChangesAsync();
        return assignment;
    }

    /// <summary>
    /// Adds an exercise to the library.
    /// </summary>
    /// <param name="name">Unique name.</param>
    /// <param name="metValue">MET value.</param>
    /// <param name="isActive">Whether it is active.</param>
    /// <returns>The saved exercise.</returns>
    public async Task<Exercise> AddExerciseAsync(string name, decimal metValue = 5m, bool isActive = true)
    {
        var exercise = new Exercise
        {
            Name = name,
            Category = ExerciseCategory.Strength,
            MuscleGroup = MuscleGroup.FullBody,
            MetValue = metValue,
            IsActive = isActive
        };
        Db.Exercises.Add(exercise);
        await Db.SaveChangesAsync();
        return exercise;
    }

    /// <summary>
    /// Records a measurement for a client.
    /// </summary>
    /// <param name="client">The client.</param>
    /// <param name="weightKg">Weight in kg.</param>
    /// <param name="measuredAt">When; now by default.</param>
    /// <returns>The saved measurement.</returns>
    public async Task<HealthMeasurement> AddMeasurementAsync(ClientProfile client, decimal weightKg, DateTime? measuredAt = null)
    {
        var measurement = new HealthMeasurement
        {
            ClientId = client.ClientId,
            WeightKg = weightKg,
            HeightCm = 170m,
            MeasuredAt = measuredAt ?? Clock.Now
        };
        Db.HealthMeasurements.Add(measurement);
        await Db.SaveChangesAsync();
        return measurement;
    }

    /// <summary>
    /// Adds a workout plan or template with lines.
    /// </summary>
    /// <param name="trainer">The author.</param>
    /// <param name="client">The client, or null for a template.</param>
    /// <param name="status">The status.</param>
    /// <param name="lines">The lines; each needs an ExerciseId and a day.</param>
    /// <returns>The saved plan, with its lines.</returns>
    public async Task<WorkoutPlan> AddPlanAsync(TrainerProfile trainer, ClientProfile? client, PlanStatus status = PlanStatus.Draft,
        params WorkoutPlanExercise[] lines)
    {
        var plan = new WorkoutPlan
        {
            TrainerId = trainer.TrainerId,
            ClientId = client?.ClientId,
            IsTemplate = client is null,
            Title = client is null ? "Template" : "Plan",
            Difficulty = DifficultyLevel.Beginner,
            StartDate = client is null ? null : Clock.Today.AddDays(-7),
            Status = status,
            CreatedAt = Clock.Now
        };
        foreach (var line in lines)
        {
            plan.Exercises.Add(line);
        }

        Db.WorkoutPlans.Add(plan);
        await Db.SaveChangesAsync();
        return plan;
    }

    /// <summary>
    /// Adds a food to the library.
    /// </summary>
    /// <param name="name">Unique name.</param>
    /// <param name="kcal">kcal per serving.</param>
    /// <param name="protein">Protein in grams per serving.</param>
    /// <param name="carbs">Carbohydrate in grams per serving.</param>
    /// <param name="fat">Fat in grams per serving.</param>
    /// <param name="isActive">Whether it is active.</param>
    /// <returns>The saved food.</returns>
    public async Task<FoodItem> AddFoodAsync(string name, decimal kcal, decimal protein = 0m, decimal carbs = 0m, decimal fat = 0m,
        bool isActive = true)
    {
        var food = new FoodItem
        {
            Name = name,
            Category = FoodCategory.Grains,
            ServingSize = 100m,
            ServingUnit = "g",
            CaloriesPerServing = kcal,
            ProteinG = protein,
            CarbsG = carbs,
            FatG = fat,
            IsActive = isActive
        };
        Db.FoodItems.Add(food);
        await Db.SaveChangesAsync();
        return food;
    }

    /// <summary>
    /// Adds a diet plan with meals.
    /// </summary>
    /// <param name="trainer">The author.</param>
    /// <param name="client">The client.</param>
    /// <param name="status">The status.</param>
    /// <param name="dailyCalories">The daily calorie target.</param>
    /// <param name="startDate">The start date; a week ago by default.</param>
    /// <param name="meals">The meals.</param>
    /// <returns>The saved plan, with its meals.</returns>
    public async Task<DietPlan> AddDietPlanAsync(TrainerProfile trainer, ClientProfile client, PlanStatus status = PlanStatus.Draft,
        int dailyCalories = 2000, DateTime? startDate = null, params DietPlanMeal[] meals)
    {
        var plan = new DietPlan
        {
            TrainerId = trainer.TrainerId,
            ClientId = client.ClientId,
            Title = "Diet",
            DailyCalorieTarget = dailyCalories,
            StartDate = startDate ?? Clock.Today.AddDays(-7),
            Status = status,
            CreatedAt = Clock.Now
        };
        foreach (var meal in meals)
        {
            plan.Meals.Add(meal);
        }

        Db.DietPlans.Add(plan);
        await Db.SaveChangesAsync();
        return plan;
    }

    /// <summary>
    /// A planned meal for <see cref="AddDietPlanAsync"/>.
    /// </summary>
    /// <param name="type">Breakfast, lunch, dinner or snack.</param>
    /// <param name="day">The weekday, or null for every day.</param>
    /// <param name="hour">The hour it is scheduled.</param>
    /// <param name="kcal">Target kcal.</param>
    /// <returns>The meal, not yet saved.</returns>
    public static DietPlanMeal Meal(MealType type, DayOfWeek? day = null, int hour = 8, int kcal = 500) =>
        new() { MealType = type, DayOfWeek = day, ScheduledTime = TimeSpan.FromHours(hour), MealDescription = type + " meal", TargetCalories = kcal };

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _root.DisposeAsync();
    }
}
