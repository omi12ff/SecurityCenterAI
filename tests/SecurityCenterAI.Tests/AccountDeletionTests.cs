using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecurityCenterAI.Domain.Entities;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Tests;

public sealed class AccountDeletionTests
{
    private static Task<HttpResponseMessage> DeleteAsync(HttpClient client, string token, string password, string confirmation = "ELIMINAR")
    {
        var request = AccountApiFactory.AuthorizedRequest(HttpMethod.Delete, "/api/account", token);
        request.Content = JsonContent.Create(new { password, confirmation });
        return client.SendAsync(request);
    }

    [Fact]
    public async Task Delete_RemovesAllOwnedRecordsAndInvalidatesAccessWithoutTouchingAnotherAccount()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var account = AccountCredentials.New();
        var owner = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, account);
        var foreign = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, AccountCredentials.New());
        var pairing = await DeviceApiTestSupport.CreateAsync(client, owner.AccessToken);
        var credential = await DeviceApiTestSupport.PairAsync(client, pairing.PairingCode);
        var foreignDevice = await DeviceApiTestSupport.CreateAsync(client, foreign.AccessToken);
        Guid analysisId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var analysis = new SecurityAnalysis { UserId = owner.User.Id, DeviceId = credential.DeviceId, Score = 75,
                Findings = [new SecurityFinding { RuleId = "WIN-TPM-READY", RuleVersion = 1, Title = "TPM", Severity = "informational",
                    Evidence = "Test", Recommendation = "Test", DetectedAtUtc = DateTime.UtcNow }] };
            analysisId = analysis.Id;
            db.SecurityAnalyses.AddRange(analysis, new SecurityAnalysis { UserId = owner.User.Id, Score = 42 },
                new SecurityAnalysis { UserId = foreign.User.Id, DeviceId = foreignDevice.Device.Id, Score = 88 });
            db.AnalysisFeedback.Add(new AnalysisFeedback { UserId = owner.User.Id, AnalysisId = analysis.Id, Rating = "helpful", Comment = "Private" });
            db.DeletedAnalysisReports.Add(new DeletedAnalysisReport { DeviceId = credential.DeviceId, ReportId = Guid.NewGuid(), DeletedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        using var response = await DeleteAsync(client, owner.AccessToken, account.Password);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("", await response.Content.ReadAsStringAsync());
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(foreign.User.Id, (await db.Users.SingleAsync()).Id);
            Assert.Equal(foreignDevice.Device.Id, (await db.Devices.SingleAsync()).Id);
            Assert.False(await db.UserSessions.AnyAsync(item => item.UserId == owner.User.Id));
            Assert.False(await db.RefreshCredentials.AnyAsync(item => item.Session.UserId == owner.User.Id));
            Assert.False(await db.AccountActionTokens.AnyAsync(item => item.UserId == owner.User.Id));
            Assert.Empty(await db.DeviceCredentials.ToListAsync());
            Assert.False(await db.DevicePairingCodes.AnyAsync(item => item.DeviceId == credential.DeviceId));
            Assert.Empty(await db.DeletedAnalysisReports.ToListAsync());
            Assert.Empty(await db.AnalysisFeedback.ToListAsync());
            Assert.Empty(await db.SecurityFindings.Where(item => item.SecurityAnalysisId == analysisId).ToListAsync());
            Assert.Equal(foreign.User.Id, (await db.SecurityAnalyses.SingleAsync()).UserId);
        }
        using var profile = await client.SendAsync(AccountApiFactory.AuthorizedRequest(HttpMethod.Get, "/api/auth/me", owner.AccessToken));
        using var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = owner.RefreshToken });
        using var heartbeat = await client.SendAsync(DeviceApiTestSupport.Heartbeat(credential.DeviceToken));
        Assert.Equal(HttpStatusCode.Unauthorized, profile.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, heartbeat.StatusCode);
    }

    [Theory]
    [InlineData("eliminar")]
    [InlineData(" ELIMINAR")]
    [InlineData("")]
    public async Task Delete_RequiresTheExactConfirmationWithoutConsumingPasswordAttempts(string confirmation)
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var account = AccountCredentials.New();
        var owner = await factory.RegisterAsync(client, account);
        using var response = await DeleteAsync(client, owner.AccessToken, account.Password, confirmation);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.SingleAsync();
        Assert.Equal(0, user.FailedLoginAttempts);
    }

    [Fact]
    public async Task Delete_WrongPasswordSharesAccountLockoutAndDoesNotLeakTheSubmittedSecret()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var account = AccountCredentials.New();
        var owner = await factory.RegisterAsync(client, account);
        const string wrong = "Synthetic-Wrong-Secret-For-Deletion";
        for (var index = 0; index < factory.SecurityOptions.MaxFailedLoginAttempts; index++)
        {
            using var failed = await DeleteAsync(client, owner.AccessToken, wrong);
            Assert.Equal(HttpStatusCode.Forbidden, failed.StatusCode);
            Assert.DoesNotContain(wrong, await failed.Content.ReadAsStringAsync());
        }
        using var locked = await DeleteAsync(client, owner.AccessToken, account.Password);
        Assert.Equal(HttpStatusCode.Forbidden, locked.StatusCode);
        using (var scope = factory.Services.CreateScope())
            Assert.Single(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.ToListAsync());
        factory.Clock.Advance(TimeSpan.FromMinutes(factory.SecurityOptions.LockoutMinutes + 1));
        // A new session is required after advancing past the original JWT lifetime.
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { account.Email, account.Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var renewed = (await login.Content.ReadFromJsonAsync<AccountAuthentication>())!;
        using var deleted = await DeleteAsync(client, renewed.AccessToken, account.Password);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("device")]
    public async Task Delete_RejectsAnonymousAndDeviceAuthentication(string scheme)
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var account = AccountCredentials.New();
        var owner = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, account);
        var code = await DeviceApiTestSupport.CreateAsync(client, owner.AccessToken);
        var credential = await DeviceApiTestSupport.PairAsync(client, code.PairingCode);
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/account")
        { Content = JsonContent.Create(new { password = account.Password, confirmation = "ELIMINAR" }) };
        if (scheme == "device") request.Headers.Authorization = new("Device", credential.DeviceToken);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Delete_RejectsExtraFieldsAndOversizedBodiesAndHasItsOwnRateLimit()
    {
        using var factory = new AccountApiFactory(accountDeletionPermitLimit: 3);
        using var client = factory.CreateApiClient();
        var account = AccountCredentials.New();
        var owner = await factory.RegisterAsync(client, account);
        using var extra = AccountApiFactory.AuthorizedRequest(HttpMethod.Delete, "/api/account", owner.AccessToken);
        extra.Content = JsonContent.Create(new { password = account.Password, confirmation = "ELIMINAR", userId = Guid.NewGuid() });
        using var rejected = await client.SendAsync(extra);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        using var big = AccountApiFactory.AuthorizedRequest(HttpMethod.Delete, "/api/account", owner.AccessToken);
        big.Content = new StringContent(new string(' ', 2050), Encoding.UTF8, "application/json");
        using var oversized = await client.SendAsync(big);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, oversized.StatusCode);
        using var failed = await DeleteAsync(client, owner.AccessToken, "WrongSyntheticPassword");
        Assert.Equal(HttpStatusCode.Forbidden, failed.StatusCode);
        using var limited = await DeleteAsync(client, owner.AccessToken, account.Password);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.NotNull(limited.Headers.RetryAfter);
        using var profile = await client.SendAsync(AccountApiFactory.AuthorizedRequest(HttpMethod.Get, "/api/auth/me", owner.AccessToken));
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
    }

    [Fact]
    public async Task Delete_AcceptsTheActualLegacyPasswordRatherThanRegistrationLengthRules()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var owner = await factory.RegisterAsync(client, AccountCredentials.New());
        using (var scope = factory.Services.CreateScope())
        {
            var user = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.SingleAsync();
            user.PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>().HashPassword(user, " x ");
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().SaveChangesAsync();
        }
        using var response = await DeleteAsync(client, owner.AccessToken, " x ");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
