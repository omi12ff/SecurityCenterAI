using System.Text;
using System.Threading.RateLimiting;
using System.Threading.Channels;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SecurityCenterAI.Api.Configuration;
using SecurityCenterAI.Api.Services;
using SecurityCenterAI.Api.Services.Backups;
using SecurityCenterAI.Api.Services.Operations;
using SecurityCenterAI.Domain.Entities;
using SecurityCenterAI.Infrastructure;
using SecurityCenterAI.Infrastructure.Persistence;

if (args.Any(arg => arg.StartsWith("--backup-", StringComparison.OrdinalIgnoreCase)))
{
    Environment.ExitCode = await BackupCommand.RunAsync(args);
    return;
}

if (args.Any(arg => arg.StartsWith("--retention-", StringComparison.OrdinalIgnoreCase)))
{
    Environment.ExitCode = await RetentionCommand.RunAsync(args);
    return;
}

var builder = WebApplication.CreateBuilder(args);
SafeOperationalLogging.Configure(builder.Logging, builder.Services);
builder.Services.AddHealthChecks().AddCheck<PostgresReadinessCheck>("postgresql",
    tags: ["ready"], timeout: TimeSpan.FromSeconds(3));

var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var jwtOptions = jwtSection.Get<JwtOptions>()
    ?? throw new InvalidOperationException("No se configuró la sección Jwt.");

if (string.IsNullOrWhiteSpace(jwtOptions.Key) || jwtOptions.Key.Length < 32 ||
    string.IsNullOrWhiteSpace(jwtOptions.Issuer) || string.IsNullOrWhiteSpace(jwtOptions.Audience) ||
    jwtOptions.ExpirationMinutes is < 1 or > 15)
{
    throw new InvalidOperationException("Configura Jwt con una clave de al menos 32 caracteres, emisor, audiencia y vigencia de 1 a 15 minutos.");
}

builder.Services.Configure<JwtOptions>(jwtSection);
EmailOptions.Validate(builder.Configuration, builder.Environment);
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.AddOptions<AuthSecurityOptions>()
    .Bind(builder.Configuration.GetSection(AuthSecurityOptions.SectionName))
    .Validate(options => options.RefreshDays is >= 1 and <= 365 && options.PasswordResetMinutes is >= 1 and <= 60 &&
        options.VerificationHours is >= 1 and <= 72 && options.MaxFailedLoginAttempts is >= 1 and <= 20 &&
        options.LockoutMinutes is >= 1 and <= 60 && options.EmailCooldownSeconds is >= 1 and <= 3600 &&
        options.MaxActiveSessions is >= 1 and <= 100 && options.EmailChangeMinutes is >= 1 and <= 30,
        "La configuración de seguridad de cuentas no es válida.").ValidateOnStart();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddOptions<RetentionOptions>()
    .Bind(builder.Configuration.GetSection(RetentionOptions.SectionName))
    .Validate(RetentionOptions.Valid, "La configuración de retención no es válida.").ValidateOnStart();
builder.Services.AddScoped<RetentionMaintenanceService>();
builder.Services.AddHostedService<RetentionWorker>();
builder.Services.AddOptions<DeviceSecurityOptions>()
    .Bind(builder.Configuration.GetSection(DeviceSecurityOptions.SectionName))
    .Validate(options => options.PairingMinutes is >= 1 and <= 10 && options.CredentialDays is >= 1 and <= 90,
        "La configuración de vinculación de dispositivos no es válida.").ValidateOnStart();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddSingleton(Channel.CreateBounded<PendingAccountEmail>(new BoundedChannelOptions(100)
{ SingleReader = true, SingleWriter = false, FullMode = BoundedChannelFullMode.Wait }));
builder.Services.AddSingleton<IAccountEmailTransport, SmtpAccountEmailSender>();
builder.Services.AddSingleton<IAccountEmailSender, QueuedAccountEmailSender>();
builder.Services.AddHostedService<AccountEmailWorker>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<DeviceService>();
builder.Services.AddScoped<AnalysisIngestionService>();
builder.Services.AddScoped<AnalysisGuidanceService>();
builder.Services.AddScoped<AnalysisComparisonService>();
builder.Services.AddScoped<AnalysisHistoryService>();
builder.Services.AddScoped<AccountExportService>();
builder.Services.AddScoped<AccountDeletionService>();
builder.Services.AddScoped<SecurityActivityService>();
builder.Services.AddScoped<AccountSessionService>();
builder.Services.AddScoped<AccountPasswordService>();
builder.Services.AddScoped<AccountProfileService>();
builder.Services.AddScoped<AccountEmailChangeService>();
builder.Services.AddOptions<LinkMessageOptions>().Bind(builder.Configuration.GetSection(LinkMessageOptions.SectionName))
    .Validate(options => options.DailyLimit is >= 1 and <= 1000, "El límite diario de enlaces y mensajes no es válido.").ValidateOnStart();
builder.Services.AddScoped<LinkMessageService>();
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
    policy.WithOrigins(builder.Configuration["FrontendUrl"] ?? "http://localhost:3000")
        .AllowAnyHeader()
        .AllowAnyMethod()));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                if (!Guid.TryParse(context.Principal?.FindFirst("sub")?.Value, out var userId) ||
                    !Guid.TryParse(context.Principal?.FindFirst("sid")?.Value, out var sessionId))
                { context.Fail("La sesión no es válida."); return; }
                var database = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var now = context.HttpContext.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
                if (!await database.UserSessions.AsNoTracking().AnyAsync(item => item.Id == sessionId && item.UserId == userId &&
                    item.RevokedAtUtc == null && item.ExpiresAtUtc > now, context.HttpContext.RequestAborted))
                    context.Fail("La sesión no es válida.");
            }
        };
    });
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>, TimeProvider>((options, configured, clock) =>
    {
        var jwt = configured.Value;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwt.Key)),
            ClockSkew = TimeSpan.FromSeconds(30),
            LifetimeValidator = (notBefore, expires, _, _) =>
            {
                var now = clock.GetUtcNow().UtcDateTime;
                return expires.HasValue && expires.Value >= now.AddSeconds(-30) &&
                    (!notBefore.HasValue || notBefore.Value <= now.AddSeconds(30));
            }
        };
    });
builder.Services.AddAuthentication()
    .AddScheme<AuthenticationSchemeOptions, DeviceAuthenticationHandler>(DeviceAuthenticationHandler.SchemeName, _ => { });
builder.Services.AddRateLimiter(options =>
{
    var window = TimeSpan.FromSeconds(builder.Configuration.GetValue("RateLimits:WindowSeconds", 60));
    FixedWindowRateLimiterOptions Limits(int permits) => new()
    { PermitLimit = permits, Window = window, QueueLimit = 0, AutoReplenishment = true };
    string Address(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(Address(context), _ => Limits(builder.Configuration.GetValue("RateLimits:GlobalPermitLimit", 120))));
    options.AddPolicy("AuthPublic", context => RateLimitPartition.GetFixedWindowLimiter(Address(context),
        _ => Limits(builder.Configuration.GetValue("RateLimits:AuthPermitLimit", 60))));
    options.AddPolicy("Readiness", context => RateLimitPartition.GetFixedWindowLimiter(Address(context),
        _ => Limits(builder.Configuration.GetValue("RateLimits:ReadinessPermitLimit", 30))));
    options.AddPolicy("AccountEmail", context => RateLimitPartition.GetFixedWindowLimiter(Address(context),
        _ => Limits(builder.Configuration.GetValue("RateLimits:EmailPermitLimit", 40))));
    options.AddPolicy("DeviceAccount", context => RateLimitPartition.GetFixedWindowLimiter(Address(context),
        _ => Limits(builder.Configuration.GetValue("RateLimits:DevicePermitLimit", 60))));
    options.AddPolicy("AccountExport", context => RateLimitPartition.GetFixedWindowLimiter(Address(context),
        _ => Limits(builder.Configuration.GetValue("RateLimits:AccountExportPermitLimit", 3))));
    options.AddPolicy("AccountDeletion", context => RateLimitPartition.GetFixedWindowLimiter(Address(context),
        _ => Limits(builder.Configuration.GetValue("RateLimits:AccountDeletionPermitLimit", 3))));
    options.AddPolicy("AccountPassword", context => RateLimitPartition.GetFixedWindowLimiter(Address(context),
        _ => Limits(builder.Configuration.GetValue("RateLimits:AccountPasswordPermitLimit", 5))));
    options.AddPolicy("AccountEmailChange", context => RateLimitPartition.GetFixedWindowLimiter(Address(context),
        _ => Limits(builder.Configuration.GetValue("RateLimits:AccountEmailChangePermitLimit", 5))));
    options.AddPolicy("AgentPairing", context => RateLimitPartition.GetFixedWindowLimiter(Address(context),
        _ => Limits(builder.Configuration.GetValue("RateLimits:AgentPairPermitLimit", 8))));
    options.AddPolicy("AgentHeartbeat", context => RateLimitPartition.GetFixedWindowLimiter(Address(context),
        _ => Limits(builder.Configuration.GetValue("RateLimits:AgentHeartbeatPermitLimit", 60))));
    options.AddPolicy("AgentAnalysis", context => RateLimitPartition.GetFixedWindowLimiter(Address(context),
        _ => Limits(builder.Configuration.GetValue("RateLimits:AnalysisPermitLimit", 6))));
    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry))
            context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retry.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
        await context.HttpContext.Response.WriteAsJsonAsync(new { title = "Demasiados intentos. Esperá un momento y volvé a intentar.", status = 429 }, cancellationToken);
    };
});
builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Security Center AI API",
        Version = "v1"
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

app.UseMiddleware<OperationalRequestMiddleware>();
app.UseRouting();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("Frontend");
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    var maximumBytes = context.GetEndpoint()?.Metadata.GetMetadata<IRequestSizeLimitMetadata>()?.MaxRequestBodySize;
    if ((!HttpMethods.IsPost(context.Request.Method) && !HttpMethods.IsDelete(context.Request.Method)) || maximumBytes is null)
    { await next(context); return; }
    async Task TooLargeAsync()
    {
        context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
        await context.Response.WriteAsJsonAsync(new { title = $"El contenido supera el límite de {maximumBytes / 1024} KiB.", status = 413 }, context.RequestAborted);
    }
    if (context.Request.ContentLength > maximumBytes) { await TooLargeAsync(); return; }
    // Bound declared and chunked bodies using endpoint metadata, before MVC.
    // Keep reports and feedback solely in memory; never spill client text to disk.
    var original = context.Request.Body;
    await using var bounded = new MemoryStream();
    var buffer = new byte[8192];
    while (true)
    {
        var read = await original.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, maximumBytes.Value + 1 - bounded.Length)), context.RequestAborted);
        if (read == 0) break;
        await bounded.WriteAsync(buffer.AsMemory(0, read), context.RequestAborted);
        if (bounded.Length > maximumBytes) { await TooLargeAsync(); return; }
    }
    bounded.Position = 0;
    context.Request.Body = bounded;
    try { await next(context); }
    finally { context.Request.Body = original; }
});
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapOperationalHealth();

app.Run();

public partial class Program;
