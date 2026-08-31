using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Domain.Entities;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Tests;

public sealed class ApiIntegrationTests(
    SecurityCenterAiApiFactory factory)
    : IClassFixture<SecurityCenterAiApiFactory>
{
    [Fact]
    public async Task Register_Profile_AndDashboard_WorkWithIssuedToken()
    {
        var email = UniqueEmail();
        var (response, authentication) = await Register(
            "  Backend User  ",
            email.ToUpperInvariant());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(authentication);
        Assert.Equal("Backend User", authentication.User.Name);
        Assert.Equal(email, authentication.User.Email);
        Assert.False(string.IsNullOrWhiteSpace(authentication.AccessToken));

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var savedUser = await dbContext.Users
                .AsNoTracking()
                .SingleAsync(user => user.Id == authentication.User.Id);

            Assert.NotEqual(ValidPassword, savedUser.PasswordHash);
        }

        using var client = CreateAuthenticatedClient(authentication.AccessToken);

        var profileResponse = await client.GetAsync("/api/profile");
        var profile = await profileResponse.Content
            .ReadFromJsonAsync<ProfileResponse>();

        Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);
        Assert.NotNull(profile);
        Assert.Equal(authentication.User.Id, profile.Id);
        Assert.Equal(authentication.User.Email, profile.Email);

        var dashboardResponse = await client.GetAsync("/api/dashboard");
        var dashboard = await dashboardResponse.Content
            .ReadFromJsonAsync<DashboardResponse>();

        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);
        Assert.NotNull(dashboard);
        Assert.Null(dashboard.SecurityScore);
        Assert.Equal(0, dashboard.TotalAnalyses);
        Assert.Empty(dashboard.RecentAnalyses);
    }

    [Fact]
    public async Task Register_InvalidPayloads_ReturnBadRequestInsteadOfServerError()
    {
        using var client = factory.CreateClient();
        var invalidPayloads = new object[]
        {
            new { name = (string?)null, email = UniqueEmail(), password = ValidPassword },
            new { name = "Valid Name", email = (string?)null, password = ValidPassword },
            new { name = " ", email = UniqueEmail(), password = ValidPassword },
            new { name = "Valid Name", email = "not-an-email", password = ValidPassword },
            new { name = "Valid Name", email = UniqueEmail(), password = "alllowercase1" },
            new { name = "Valid Name", email = UniqueEmail(), password = "NoNumberHere" }
        };

        foreach (var payload in invalidPayloads)
        {
            var response = await client.PostAsJsonAsync(
                "/api/auth/register",
                payload);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task DuplicateEmail_IsCaseInsensitive_AndReturnsConflict()
    {
        var email = UniqueEmail();
        var (firstResponse, _) = await Register("First User", email);
        var (secondResponse, _) = await Register(
            "Second User",
            email.ToUpperInvariant());

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Fact]
    public async Task Login_ValidatesCredentials_AndReturnsAnUsableToken()
    {
        var email = UniqueEmail();
        await Register("Login User", email);
        using var client = factory.CreateClient();

        var wrongPassword = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = "WrongPass123" });
        var missingUser = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = UniqueEmail(), password = "WrongPass123" });
        var validLogin = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = ValidPassword });
        var authentication = await validLogin.Content
            .ReadFromJsonAsync<AuthResponse>();

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, missingUser.StatusCode);
        Assert.Equal(HttpStatusCode.OK, validLogin.StatusCode);
        Assert.NotNull(authentication);

        using var authenticatedClient =
            CreateAuthenticatedClient(authentication.AccessToken);
        Assert.Equal(
            HttpStatusCode.OK,
            (await authenticatedClient.GetAsync("/api/dashboard")).StatusCode);
    }

    [Fact]
    public async Task Dashboard_ReturnsLatestFive_AndNeverLeaksAnotherUser()
    {
        var (_, firstUser) = await Register("First User", UniqueEmail());
        var (_, secondUser) = await Register("Second User", UniqueEmail());
        Assert.NotNull(firstUser);
        Assert.NotNull(secondUser);

        var start = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            for (var index = 0; index < 7; index++)
            {
                dbContext.SecurityAnalyses.Add(
                    new SecurityAnalysis(
                        firstUser.User.Id,
                        40 + index,
                        start.AddMinutes(index)));
            }

            dbContext.SecurityAnalyses.Add(
                new SecurityAnalysis(secondUser.User.Id, 99, start.AddDays(1)));
            await dbContext.SaveChangesAsync();
        }

        using var client = CreateAuthenticatedClient(firstUser.AccessToken);
        var response = await client.GetAsync("/api/dashboard");
        var dashboard = await response.Content
            .ReadFromJsonAsync<DashboardResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(dashboard);
        Assert.Equal(7, dashboard.TotalAnalyses);
        Assert.Equal(46, dashboard.SecurityScore);
        Assert.Equal([46, 45, 44, 43, 42],
            dashboard.RecentAnalyses.Select(item => item.Score));
        Assert.DoesNotContain(
            dashboard.RecentAnalyses,
            item => item.Score == 99);
    }

    [Fact]
    public async Task ProtectedEndpoints_RejectMissingOrMalformedTokens()
    {
        using var anonymousClient = factory.CreateClient();
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anonymousClient.GetAsync("/api/profile")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anonymousClient.GetAsync("/api/dashboard")).StatusCode);

        anonymousClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "not-a-jwt");
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anonymousClient.GetAsync("/api/dashboard")).StatusCode);
    }

    [Fact]
    public async Task HealthEndpoints_ReportTheApplicationAndDatabase()
    {
        using var client = factory.CreateClient();

        Assert.Equal(
            HttpStatusCode.OK,
            (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.GetAsync("/health/ready")).StatusCode);
    }

    [Fact]
    public async Task Readiness_ReturnsServiceUnavailable_WhenDatabaseIsUnavailable()
    {
        using var unavailableDatabaseFactory =
            new UnavailableDatabaseApiFactory();
        using var client = unavailableDatabaseFactory.CreateClient();

        Assert.Equal(
            HttpStatusCode.OK,
            (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(
            HttpStatusCode.ServiceUnavailable,
            (await client.GetAsync("/health/ready")).StatusCode);
    }

    private async Task<(HttpResponseMessage Response, AuthResponse? Authentication)>
        Register(string name, string email)
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { name, email, password = ValidPassword });

        var authentication = response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AuthResponse>()
            : null;

        return (response, authentication);
    }

    private HttpClient CreateAuthenticatedClient(string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static string UniqueEmail()
    {
        return $"{Guid.NewGuid():N}@example.com";
    }

    private const string ValidPassword = "SecurePass123";
}
