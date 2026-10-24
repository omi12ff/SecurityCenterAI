using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Api.Services;
using SecurityCenterAI.Domain.Entities;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Tests;

public sealed class SecurityActivityTests
{
    private const string Path = "/api/account/security-activity";

    [Fact]
    public async Task FilteredPaginationPreservesTiesAndIsolationWhenNewActivityArrives()
    {
        using var factory = new AccountApiFactory();
        await VerifyFilteredPaginationAsync(factory);
    }

    internal static async Task VerifyFilteredPaginationAsync(AccountApiFactory factory)
    {
        using var client = factory.CreateApiClient();
        var owner = await factory.RegisterAsync(client, AccountCredentials.New());
        var foreign = await factory.RegisterAsync(client, AccountCredentials.New());
        var day = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc);
        var tied = Enumerable.Range(1, 125).Select(index => new SecurityActivity
        {
            Id = Guid.Parse($"{index:x8}-1111-4444-8888-111111111111"), UserId = owner.User.Id,
            Kind = SecurityActivityKind.SessionStarted, OccurredAtUtc = day.AddHours(12)
        }).ToArray();
        var start = new SecurityActivity { UserId = owner.User.Id, Kind = SecurityActivityKind.PasswordReset, OccurredAtUtc = day };
        var end = new SecurityActivity { UserId = owner.User.Id, Kind = SecurityActivityKind.PasswordReset, OccurredAtUtc = day.AddDays(1).AddTicks(-10) };
        var foreignRow = new SecurityActivity { UserId = foreign.User.Id, Kind = SecurityActivityKind.SessionStarted, OccurredAtUtc = day.AddHours(12) };
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.SecurityActivities.AddRange(tied); db.SecurityActivities.AddRange(start, end, foreignRow,
                new() { UserId = owner.User.Id, Kind = SecurityActivityKind.PasswordReset, OccurredAtUtc = day.AddDays(-1) },
                new() { UserId = owner.User.Id, Kind = SecurityActivityKind.PasswordReset, OccurredAtUtc = day.AddDays(1) });
            await db.SaveChangesAsync();
        }
        const string filter = "?action=session.started&from=2026-09-10&to=2026-09-10";
        var first = await ReadAsync(client, owner.AccessToken, filter);
        Assert.Equal(50, first.Events.Count); Assert.True(first.HasMore); Assert.Equal(first.Events[^1].Id, first.NextBefore);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // A newer matching event must not shift subsequent pages or reappear.
            SecurityActivityRecorder.Add(db, owner.User.Id, SecurityActivityKind.SessionStarted, day.AddHours(13));
            await db.SaveChangesAsync();
        }
        var second = await ReadAsync(client, owner.AccessToken, filter + "&before=" + first.NextBefore);
        var third = await ReadAsync(client, owner.AccessToken, filter + "&before=" + second.NextBefore);
        Assert.Equal(50, second.Events.Count); Assert.True(second.HasMore);
        Assert.Equal(25, third.Events.Count); Assert.False(third.HasMore); Assert.Null(third.NextBefore);
        var ids = first.Events.Concat(second.Events).Concat(third.Events).Select(item => item.Id).ToArray();
        Assert.Equal(tied.Select(item => item.Id).OrderByDescending(item => item), ids);
        Assert.Equal(125, ids.Distinct().Count());
        var boundary = await ReadAsync(client, owner.AccessToken, "?action=password.reset&from=2026-09-10&to=2026-09-10");
        Assert.Equal(new[] { end.Id, start.Id }, boundary.Events.Select(item => item.Id)); Assert.Null(boundary.NextBefore);
        var empty = await ReadAsync(client, owner.AccessToken, "?action=email.changed&from=2026-09-10");
        Assert.Empty(empty.Events); Assert.False(empty.HasMore); Assert.Null(empty.NextBefore);
        foreach (var anchor in new[] { foreignRow.Id, Guid.NewGuid(), start.Id })
        {
            using var request = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, Path + filter + "&before=" + anchor, owner.AccessToken);
            using var response = await client.SendAsync(request); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.True(response.Headers.CacheControl!.NoStore);
        }
        using var check = factory.Services.CreateScope(); var database = check.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(131, await database.SecurityActivities.CountAsync(item => item.UserId == owner.User.Id));
    }

    [Fact]
    public async Task InvalidActivityFiltersAreRejectedWithoutEchoingInputOrWritingEvents()
    {
        using var factory = new AccountApiFactory(); using var client = factory.CreateApiClient();
        var owner = await factory.RegisterAsync(client, AccountCredentials.New());
        foreach (var query in new[] { "action=PRIVATE_INVALID", "action=0", "action=SessionStarted", "from=2026-02-30", "from=0000-01-01",
            "from=2026-09-11&to=2026-09-10", "to=9999-12-31", "to=2026-09-10T00%3A00%3A00Z", "before=PRIVATE_INVALID",
            "before=00000000-0000-0000-0000-000000000000", "action=session.started&action=password.reset", "from=2026-09-10&from=2026-09-11" })
        {
            using var request = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, Path + "?" + query, owner.AccessToken);
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); Assert.True(response.Headers.CacheControl!.NoStore);
            Assert.DoesNotContain("PRIVATE_INVALID", await response.Content.ReadAsStringAsync());
        }
        Assert.Single((await ReadAsync(client, owner.AccessToken)).Events);
    }

    private static async Task<SecurityActivityListResponse> ReadAsync(HttpClient client, string token, string suffix = "")
    {
        using var request = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, Path + suffix, token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        return (await response.Content.ReadFromJsonAsync<SecurityActivityListResponse>())!;
    }
    private static async Task<AccountAuthentication> LoginAsync(HttpClient client, AccountCredentials account)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { account.Email, account.Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AccountAuthentication>())!;
    }

    [Fact]
    public async Task ActivityIsPrivateAndDoesNotContainAccountOrAuthenticationMaterial()
    {
        using var factory = new AccountApiFactory(); using var client = factory.CreateApiClient();
        var account = AccountCredentials.New(); var owner = await factory.RegisterAsync(client, account);
        var foreign = await factory.RegisterAsync(client, AccountCredentials.New());
        var own = await ReadAsync(client, owner.AccessToken, "?userId=" + foreign.User.Id);
        Assert.Equal("account.registered", Assert.Single(own.Events).Action);
        Assert.Equal(factory.Clock.GetUtcNow().UtcDateTime, own.Events[0].OccurredAtUtc);
        var other = await ReadAsync(client, foreign.AccessToken);
        Assert.NotEqual(own.Events[0].Id, other.Events[0].Id);
        var json = JsonSerializer.Serialize(own);
        foreach (var secret in new[] { account.Email, account.Password, owner.AccessToken, owner.RefreshToken, owner.User.Id.ToString() }) Assert.DoesNotContain(secret, json);
        using var plain = await client.GetAsync(Path); Assert.Equal(HttpStatusCode.Unauthorized, plain.StatusCode);
        using var post = AccountApiFactory.AuthorizedRequest(HttpMethod.Post, Path, owner.AccessToken);
        post.Content = JsonContent.Create(new { action = "password.reset" });
        using var rejected = await client.SendAsync(post); Assert.Equal(HttpStatusCode.MethodNotAllowed, rejected.StatusCode);
    }

    [Fact]
    public async Task LoginLogoutAndRefreshReplayAreRecordedOnceButNormalRefreshAndFailuresAreNot()
    {
        using var factory = new AccountApiFactory(); using var client = factory.CreateApiClient();
        var account = AccountCredentials.New(); var owner = await factory.RegisterAsync(client, account);
        using (var bad = await client.PostAsJsonAsync("/api/auth/login", new { account.Email, Password = "wrong-password" })) Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        var login = await LoginAsync(client, account);
        using (var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { login.RefreshToken })) Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        using (var replay = await client.PostAsJsonAsync("/api/auth/refresh", new { login.RefreshToken })) Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        using (var again = await client.PostAsJsonAsync("/api/auth/refresh", new { login.RefreshToken })) Assert.Equal(HttpStatusCode.Unauthorized, again.StatusCode);
        var actions = (await ReadAsync(client, owner.AccessToken)).Events.Select(item => item.Action).ToArray();
        Assert.Equal(3, actions.Length); Assert.Single(actions, item => item == "session.replay-blocked");
        using (var logout = await client.PostAsJsonAsync("/api/auth/logout", new { owner.RefreshToken })) Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        using (var logout = await client.PostAsJsonAsync("/api/auth/logout", new { owner.RefreshToken })) Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        var fresh = await LoginAsync(client, account);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.SecurityActivities.CountAsync(item => item.UserId == owner.User.Id && item.Kind == SecurityActivityKind.SessionRevoked));
        using var all = AccountApiFactory.AuthorizedRequest(HttpMethod.Post, "/api/auth/logout-all", fresh.AccessToken);
        using var allResponse = await client.SendAsync(all); Assert.Equal(HttpStatusCode.NoContent, allResponse.StatusCode);
        Assert.Equal(1, await db.SecurityActivities.CountAsync(item => item.UserId == owner.User.Id && item.Kind == SecurityActivityKind.AllSessionsRevoked));
    }

    [Fact]
    public async Task DeviceChangesAndPasswordRecoveryHaveNoCodesOrDeviceIdentifiers()
    {
        using var factory = new AccountApiFactory(); using var client = factory.CreateApiClient();
        var account = AccountCredentials.New(); var owner = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, account);
        var code = await DeviceApiTestSupport.CreateAsync(client, owner.AccessToken);
        using var reissue = AccountApiFactory.AuthorizedRequest(HttpMethod.Post, $"/api/devices/{code.Device.Id}/pairing-code", owner.AccessToken);
        using var response = await client.SendAsync(reissue); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var replacement = (await response.Content.ReadFromJsonAsync<TestDevicePairingCode>())!;
        var credential = await DeviceApiTestSupport.PairAsync(client, replacement.PairingCode);
        using var deviceRequest = new HttpRequestMessage(HttpMethod.Get, Path);
        deviceRequest.Headers.Authorization = new("Device", credential.DeviceToken);
        using var denied = await client.SendAsync(deviceRequest); Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        for (var iteration = 0; iteration < 2; iteration++)
        {
            using var revoke = AccountApiFactory.AuthorizedRequest(HttpMethod.Post, $"/api/devices/{code.Device.Id}/revoke", owner.AccessToken);
            using var revoked = await client.SendAsync(revoke); Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        }
        var activity = await ReadAsync(client, owner.AccessToken);
        Assert.Equal(6, activity.Events.Count); Assert.Single(activity.Events, item => item.Action == "device.revoked");
        var json = JsonSerializer.Serialize(activity);
        foreach (var value in new[] { code.PairingCode, replacement.PairingCode, credential.DeviceToken, credential.DeviceId.ToString(), account.Email }) Assert.DoesNotContain(value, json);
        using (var forgot = await client.PostAsJsonAsync("/api/auth/forgot-password", new { account.Email })) Assert.Equal(HttpStatusCode.OK, forgot.StatusCode);
        var reset = factory.Email.TokenFor(account.Email, AccountEmailPurpose.PasswordReset);
        using (var done = await client.PostAsJsonAsync("/api/auth/reset-password", new { Token = reset, Password = account.Password })) Assert.Equal(HttpStatusCode.NoContent, done.StatusCode);
        using (var again = await client.PostAsJsonAsync("/api/auth/reset-password", new { Token = reset, Password = account.Password })) Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        var fresh = await LoginAsync(client, account);
        Assert.Single((await ReadAsync(client, fresh.AccessToken)).Events, item => item.Action == "password.reset");
    }

    [Fact]
    public async Task ListIsBoundedButAccountExportContainsAllActivity()
    {
        using var factory = new AccountApiFactory(); using var client = factory.CreateApiClient();
        var owner = await factory.RegisterAsync(client, AccountCredentials.New());
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            for (var index = 0; index < 65; index++) SecurityActivityRecorder.Add(db, owner.User.Id, SecurityActivityKind.SessionStarted, factory.Clock.GetUtcNow().UtcDateTime.AddSeconds(-index - 1));
            await db.SaveChangesAsync();
        }
        var recent = await ReadAsync(client, owner.AccessToken);
        Assert.Equal(50, recent.Events.Count); Assert.True(recent.HasMore); Assert.Equal(50, recent.Limit);
        Assert.Equal("account.registered", recent.Events[0].Action);
        using var request = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, "/api/account/export", owner.AccessToken);
        using var response = await client.SendAsync(request);
        var export = (await response.Content.ReadFromJsonAsync<AccountExportResponse>())!;
        Assert.Equal(66, export.SecurityActivity!.Count);
    }

    [Fact]
    public async Task AnalysisDeletionRecordsOnlyItsKindAndAccountDeletionCascadesAllOwnEvents()
    {
        using var factory = new AccountApiFactory(); using var client = factory.CreateApiClient();
        var account = AccountCredentials.New(); var owner = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, account);
        var foreign = await factory.RegisterAsync(client, AccountCredentials.New());
        var code = await DeviceApiTestSupport.CreateAsync(client, owner.AccessToken);
        var analysis = new SecurityAnalysis { UserId = owner.User.Id, DeviceId = code.Device.Id, Score = 42, ReportId = Guid.NewGuid() };
        using (var scope = factory.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); db.SecurityAnalyses.Add(analysis); await db.SaveChangesAsync(); }
        using var deletion = AccountApiFactory.AuthorizedRequest(HttpMethod.Delete, $"/api/devices/{code.Device.Id}/analyses/{analysis.Id}", owner.AccessToken);
        deletion.Content = JsonContent.Create(new { confirmed = true });
        using var deleted = await client.SendAsync(deletion); Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        var history = await ReadAsync(client, owner.AccessToken); Assert.Single(history.Events, item => item.Action == "analysis.deleted");
        var json = JsonSerializer.Serialize(history); Assert.DoesNotContain(analysis.Id.ToString(), json); Assert.DoesNotContain(analysis.ReportId.ToString()!, json);
        using var remove = AccountApiFactory.AuthorizedRequest(HttpMethod.Delete, "/api/account", owner.AccessToken);
        remove.Content = JsonContent.Create(new { account.Password, confirmation = "ELIMINAR" });
        using var removed = await client.SendAsync(remove); Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        using var check = factory.Services.CreateScope(); var database = check.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await database.SecurityActivities.AnyAsync(item => item.UserId == owner.User.Id));
        Assert.True(await database.SecurityActivities.AnyAsync(item => item.UserId == foreign.User.Id));
    }

    [Fact]
    public async Task ExistingEntriesCannotBeEditedOrDeletedThroughTrackedWrites()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var user = new User { Name = "Ficticio", Email = "fixture@example.invalid", PasswordHash = "FICTITIOUS" };
        db.Users.Add(user); SecurityActivityRecorder.Add(db, user.Id, SecurityActivityKind.AccountRegistered, DateTime.UtcNow); await db.SaveChangesAsync();
        var row = await db.SecurityActivities.SingleAsync();
        db.Entry(row).Property(item => item.Kind).CurrentValue = SecurityActivityKind.PasswordReset;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear(); row = await db.SecurityActivities.SingleAsync(); db.SecurityActivities.Remove(row);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear(); user = await db.Users.SingleAsync(); await db.SecurityActivities.LoadAsync(); db.Users.Remove(user);
        await db.SaveChangesAsync(); Assert.Empty(await db.SecurityActivities.ToListAsync());
    }
}
