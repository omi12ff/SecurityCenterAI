using System.Globalization;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using SecurityCenterAI.Api.Configuration;
using SecurityCenterAI.Api.Services;
using SecurityCenterAI.Domain.Entities;
using SecurityCenterAI.Infrastructure;
using SecurityCenterAI.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
builder.Services
    .AddOptions<JwtOptions>()
    .Bind(jwtSection)
    .ValidateOnStart();
builder.Services.AddSingleton<
    IValidateOptions<JwtOptions>,
    JwtOptionsValidator>();
builder.Services.AddInfrastructure();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
    policy.WithOrigins(builder.Configuration["FrontendUrl"] ?? "http://localhost:3000")
        .AllowAnyHeader()
        .AllowAnyMethod()));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(
        "registration",
        context => CreateRateLimitPartition(
            context,
            builder.Configuration.GetValue("RateLimiting:RegistrationPermitLimit", 3),
            builder.Configuration.GetValue("RateLimiting:RegistrationWindowSeconds", 600)));
    options.AddPolicy(
        "login",
        context => CreateRateLimitPartition(
            context,
            builder.Configuration.GetValue("RateLimiting:LoginPermitLimit", 5),
            builder.Configuration.GetValue("RateLimiting:LoginWindowSeconds", 60)));
    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(
                MetadataName.RetryAfter,
                out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                Math.Ceiling(retryAfter.TotalSeconds)
                    .ToString(CultureInfo.InvariantCulture);
        }

        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Title = "Demasiadas solicitudes.",
                Detail = "Espera un momento antes de volver a intentarlo.",
                Status = StatusCodes.Status429TooManyRequests
            },
            cancellationToken: cancellationToken);
    };
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();
builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((options, jwtOptionsAccessor) =>
    {
        var jwtOptions = jwtOptionsAccessor.Value;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtOptions.Key)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
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

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment() &&
    !app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}
app.UseRouting();
app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health/live", () => Results.Ok(new { status = "healthy" }))
    .AllowAnonymous();
app.MapGet("/health/ready", CheckDatabase)
    .AllowAnonymous();
app.MapGet("/health", CheckDatabase)
    .AllowAnonymous();

app.Run();

static RateLimitPartition<string> CreateRateLimitPartition(
    HttpContext context,
    int permitLimit,
    int windowSeconds)
{
    return RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Max(1, permitLimit),
            Window = TimeSpan.FromSeconds(Math.Max(1, windowSeconds)),
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        });
}

static async Task<IResult> CheckDatabase(
    IServiceProvider serviceProvider,
    CancellationToken cancellationToken)
{
    try
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext =
            scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (await dbContext.Database.CanConnectAsync(cancellationToken))
        {
            await dbContext.Users
                .AsNoTracking()
                .AnyAsync(cancellationToken);
            await dbContext.SecurityAnalyses
                .AsNoTracking()
                .AnyAsync(cancellationToken);

            return Results.Ok(new { status = "healthy", database = "healthy" });
        }
    }
    catch
    {
        // No se expone información interna de la conexión en la respuesta.
    }

    return Results.Json(
        new { status = "unhealthy", database = "unhealthy" },
        statusCode: StatusCodes.Status503ServiceUnavailable);
}

public partial class Program;
