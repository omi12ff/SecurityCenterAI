using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Domain.Entities;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Tests;

public sealed class DashboardAuthenticationTests
{
    [Fact]
    public async Task RegisteredUser_CanLogInAndReadOnlyTheirRecentAnalyses()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        const string email = "dashboard@example.com";
        const string password = "TestPassword123!";
        using var registrationResponse = await client.PostAsJsonAsync(
            "/api/auth/register", new RegisterRequest("Dashboard User", email, password));
        Assert.Equal(HttpStatusCode.Created, registrationResponse.StatusCode);
        var registration = await registrationResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(registration);

        var analyzedAt = DateTime.UtcNow;
        var analyses = Enumerable.Range(0, 7)
            .Select(index => new SecurityAnalysis
            {
                UserId = registration.User.Id,
                Score = 90 - index,
                AnalyzedAtUtc = analyzedAt.AddMinutes(-index)
            })
            .ToArray();

        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.SecurityAnalyses.AddRange(analyses);
            var otherUser = new User
            {
                Name = "Other User",
                Email = "other@example.com",
                PasswordHash = "not-used-by-this-test"
            };
            database.Users.Add(otherUser);
            database.SecurityAnalyses.Add(new SecurityAnalysis
            {
                UserId = otherUser.Id,
                Score = 1,
                AnalyzedAtUtc = analyzedAt.AddMinutes(1)
            });
            await database.SaveChangesAsync();
        }

        using var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var login = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(login);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        using var dashboardResponse = await client.GetAsync("/api/dashboard");
        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);
        var dashboard = await dashboardResponse.Content.ReadFromJsonAsync<DashboardResponse>();
        Assert.NotNull(dashboard);
        Assert.Equal(90, dashboard.SecurityScore);
        Assert.Equal(7, dashboard.TotalAnalyses);
        Assert.Equal(analyses.Take(5).Select(analysis => analysis.Id),
            dashboard.RecentAnalyses.Select(analysis => analysis.Id));
    }

    [Theory]
    [InlineData("/api/auth/register")]
    [InlineData("/api/auth/login")]
    public async Task Authentication_RejectsInvalidInput(string endpoint)
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        using var response = await client.PostAsJsonAsync(endpoint,
            new { Name = "A", Email = "not-an-email", Password = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid-token")]
    public async Task Dashboard_RejectsMissingOrInvalidToken(string? token)
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using var response = await client.GetAsync("/api/dashboard");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed record DashboardResponse(
        int? SecurityScore,
        int TotalAnalyses,
        RecentAnalysis[] RecentAnalyses);

    private sealed record RecentAnalysis(Guid Id, int Score, DateTime AnalyzedAtUtc);

}
