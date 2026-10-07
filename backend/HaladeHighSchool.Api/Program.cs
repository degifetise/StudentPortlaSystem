using System.Text;
using System.Security.Claims;
using System.Text.Json.Serialization;
using HaladeHighSchool.Api.Configuration;
using HaladeHighSchool.Api.Data;
using HaladeHighSchool.Api.Models;
using HaladeHighSchool.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using FluentValidation;
using FluentValidation.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

const string CorsPolicy = "HaladeFrontend";

// ---------------------------------------------------------------------------
// Configuration
// ---------------------------------------------------------------------------
builder.Services
    .AddOptions<JwtSettings>()
    .Bind(builder.Configuration.GetSection(JwtSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<ProvisioningSettings>()
    .Bind(builder.Configuration.GetSection(ProvisioningSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<EmailSettings>()
    .Bind(builder.Configuration.GetSection(EmailSettings.SectionName))
    .Validate(settings => string.IsNullOrWhiteSpace(settings.SendGridApiKey) ||
        (!string.IsNullOrWhiteSpace(settings.FromAddress) && !string.IsNullOrWhiteSpace(settings.FromName)),
        "Email:FromAddress and Email:FromName are required when Email:SendGridApiKey is configured.")
    .ValidateOnStart();

var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
    ?? throw new InvalidOperationException("The 'Jwt' configuration section is missing.");

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

// ---------------------------------------------------------------------------
// Database
// ---------------------------------------------------------------------------
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(connectionString, sql =>
    {
        sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
        sql.CommandTimeout(60);
    });

    if (builder.Environment.IsDevelopment())
    {
        options.EnableDetailedErrors();
    }
});

builder.Services.AddScoped<IApplicationDbContext>(serviceProvider =>
    serviceProvider.GetRequiredService<ApplicationDbContext>());

// ---------------------------------------------------------------------------
// Identity
// ---------------------------------------------------------------------------
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredLength = 8;

        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = false;

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// ---------------------------------------------------------------------------
// JWT bearer authentication
// ---------------------------------------------------------------------------
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.SaveToken = true;
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
            ValidateLifetime = true,
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (string.IsNullOrWhiteSpace(context.Token)
                    && context.Request.Cookies.TryGetValue("access_token", out var cookieToken))
                {
                    context.Token = cookieToken;
                }

                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                if (context.Exception is SecurityTokenExpiredException)
                {
                    context.Response.Headers.Append("X-Token-Expired", "true");
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ---------------------------------------------------------------------------
// CORS for the Vite dev server
// ---------------------------------------------------------------------------
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173"];

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .WithExposedHeaders("X-Token-Expired", "Content-Disposition")
        .AllowCredentials());
});

// ---------------------------------------------------------------------------
// Application services
// ---------------------------------------------------------------------------
builder.Services.AddMemoryCache();

builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAccountProvisioningService, AccountProvisioningService>();
builder.Services.AddScoped<SmartIDCardService>();
builder.Services.AddSingleton<IProfilePhotoStorage, ProfilePhotoStorage>();
builder.Services.AddSingleton(serviceProvider =>
{
    var settings = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<JwtSettings>>().Value;
    return new SmartIDTokenKeyProtector(settings.Key);
});
builder.Services.AddScoped<ISystemSettingsService, SystemSettingsService>();
builder.Services.AddScoped<IGradingPolicyService, GradingPolicyService>();
builder.Services.AddScoped<ITeachingAssignmentService, TeachingAssignmentService>();
builder.Services.AddScoped<IRegistrationRequestService, RegistrationRequestService>();
builder.Services.AddScoped<IReportCardService, ReportCardService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IPdfReportCardService, PdfReportCardService>();
builder.Services.AddScoped<IReportPdfService, ReportPdfService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();
builder.Services.AddScoped<IEmailSender, SendGridEmailSender>();
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddSingleton<ILessonFileStorage, LessonFileStorage>();

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddFluentValidationClientsideAdapters();
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddAutoMapper(typeof(Program).Assembly);
builder.Services.AddValidatorsFromAssemblyContaining<HaladeHighSchool.Api.Validation.CreateEventDtoValidator>();

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();

// ---------------------------------------------------------------------------
// Swagger with a bearer token input
// ---------------------------------------------------------------------------
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "School Management System API",
        Version = "v1",
        Description = "School management API for Nursery through Grade 12: authentication, students, marks, lessons and announcements."
    });

    options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Paste the JWT access token returned by /api/auth/login. The 'Bearer ' prefix is added automatically.",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme, document)] = []
    });
});

var app = builder.Build();

// ---------------------------------------------------------------------------
// Pipeline
// ---------------------------------------------------------------------------
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "School Management System API v1");
        options.DocumentTitle = "School Management System API";
    });
}
else
{
    app.UseHttpsRedirection();
}

// Uploaded images are public card assets. Finalize their CORS headers after the
// credentialed API policy runs, so it cannot replace the wildcard for canvas reads.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/uploads"))
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["Access-Control-Allow-Origin"] = "*";
            context.Response.Headers["Access-Control-Allow-Headers"] = "*";
            return Task.CompletedTask;
        });
    }

    await next();
});

app.UseCors(CorsPolicy);
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ---------------------------------------------------------------------------
// Start-up checks and seeding
// ---------------------------------------------------------------------------
try
{
    using (var scope = app.Services.CreateScope())
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (!await db.Database.CanConnectAsync())
        {
            logger.LogCritical(
                "Cannot connect to HaladeHighSchoolDb. Run database/01_Create_HaladeHighSchoolDb.sql first.");
            throw new InvalidOperationException("HaladeHighSchoolDb is unreachable.");
        }

        try
        {
            // This project uses checked-in SQL scripts rather than EF migrations. Probe the
            // tables used by analytics, rosters, notifications and student engagement so a partial database fails at
            // startup with a useful diagnostic instead of surfacing as a request-time 500.
            await db.Users.AsNoTracking().OrderBy(user => user.Id).Select(user => user.Id).Take(0).ToListAsync();
            await db.Users.AsNoTracking().OrderBy(user => user.Id).Select(user => user.PhotoUrl).Take(0).ToListAsync();
            await db.Users.AsNoTracking().OrderBy(user => user.Id).Select(user => user.DigitalSignatureUrl).Take(0).ToListAsync();
            await db.StudentRegistrationRequests.AsNoTracking()
                .OrderBy(request => request.Id)
                .Select(request => request.PhotoUrl)
                .Take(0)
                .ToListAsync();
            await db.Teachers.AsNoTracking().OrderBy(t => t.Id).Select(t => t.Id).Take(0).ToListAsync();
            await db.Students.AsNoTracking().OrderBy(s => s.Id).Select(s => s.Id).Take(0).ToListAsync();
            await db.SmartCards.AsNoTracking().OrderBy(card => card.CardId).Select(card => card.CardId).Take(0).ToListAsync();
            await db.SmartIDScanLogs.AsNoTracking().OrderBy(log => log.Id).Select(log => log.Id).Take(0).ToListAsync();
            await db.FeeInvoices.AsNoTracking().OrderBy(invoice => invoice.Id).Select(invoice => invoice.Id).Take(0).ToListAsync();
            await db.FeePayments.AsNoTracking().OrderBy(payment => payment.Id).Select(payment => payment.Id).Take(0).ToListAsync();
            await db.Sections.AsNoTracking().OrderBy(s => s.Id).Select(s => s.Id).Take(0).ToListAsync();
            await db.TeacherSubjects.AsNoTracking().OrderBy(ts => ts.Id).Select(ts => ts.Id).Take(0).ToListAsync();
            await db.Assessments.AsNoTracking().OrderBy(assessment => assessment.Id).Select(assessment => assessment.Id).Take(0).ToListAsync();
            await db.Attendance.AsNoTracking().OrderBy(attendance => attendance.Id).Select(attendance => attendance.Id).Take(0).ToListAsync();
            await db.Events.AsNoTracking().OrderBy(schoolEvent => schoolEvent.Id).Select(schoolEvent => schoolEvent.Id).Take(0).ToListAsync();
            await db.Notifications.AsNoTracking().OrderBy(notification => notification.Id).Select(notification => notification.Id).Take(0).ToListAsync();
            await db.StudentFeedbacks.AsNoTracking().OrderBy(feedback => feedback.Id).Select(feedback => feedback.Id).Take(0).ToListAsync();
            await db.EventComments.AsNoTracking().OrderBy(comment => comment.Id).Select(comment => comment.Id).Take(0).ToListAsync();
            await db.StudentSubjectPerformances.AsNoTracking()
                .OrderBy(performance => performance.StudentId)
                .ThenBy(performance => performance.SubjectId)
                .Select(performance => performance.StudentId)
                .Take(0)
                .ToListAsync();
            logger.LogInformation("Analytics and teacher roster database schema checks passed.");
        }
        catch (Exception ex)
        {
            logger.LogCritical(
                ex,
                "Required analytics, notification, feedback, event-comment, Smart ID tables, or profile-photo columns are missing or inaccessible. Apply the numbered database SQL scripts before starting the API.");
            throw new InvalidOperationException("HaladeHighSchoolDb schema is incomplete.", ex);
        }

        await DbSeeder.SeedAsync(scope.ServiceProvider);
    }

    app.Run();
}
catch (Exception ex)
{
    Console.WriteLine($"[STARTUP CRASH ERROR]: {ex.Message}");
    if (ex.InnerException is not null)
    {
        Console.WriteLine($"[INNER EXCEPTION]: {ex.InnerException.Message}");
    }

    throw;
}
