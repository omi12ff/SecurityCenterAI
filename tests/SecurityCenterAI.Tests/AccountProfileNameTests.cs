using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecurityCenterAI.Api.Services;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Tests;

public sealed class AccountProfileNameTests
{
    internal const string Path = "/api/account/profile/name";
    internal static async Task<HttpResponseMessage> RenameAsync(HttpClient client, string token, object body)
    {
        using var request = AccountApiFactory.AuthorizedRequest(HttpMethod.Post, Path, token);
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task RenameUsesTheAuthenticatedOwnerAndCannotOverwriteOtherProfileOrAuthenticationFields()
    {
        using var factory = new AccountApiFactory(); using var client = factory.CreateApiClient();
        var account = AccountCredentials.New(); var owner = await factory.RegisterAsync(client, account);
        var foreign = await factory.RegisterAsync(client, AccountCredentials.New());
        using var beforeScope = factory.Services.CreateScope(); var beforeDb = beforeScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var before = await beforeDb.Users.AsNoTracking().SingleAsync(item => item.Id == owner.User.Id);
        var originalSession = await beforeDb.UserSessions.AsNoTracking().SingleAsync(item => item.UserId == owner.User.Id);
        var events = await beforeDb.SecurityActivities.CountAsync();
        using var changed = await RenameAsync(client, owner.AccessToken, new { name = "  María del Mar  ",
            userId = foreign.User.Id, email = "forged@example.invalid", passwordHash = "forged", emailVerified = true,
            failedLoginAttempts = 999, expiresAtUtc = DateTime.UtcNow.AddYears(1) });
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode); Assert.True(changed.Headers.CacheControl?.NoStore);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(item => item.Id == owner.User.Id);
        Assert.Equal("María del Mar", user.Name); Assert.Equal(before.Email, user.Email);
        Assert.Equal(before.PasswordHash, user.PasswordHash); Assert.Equal(before.EmailVerified, user.EmailVerified);
        Assert.Equal(before.FailedLoginAttempts, user.FailedLoginAttempts); Assert.Equal(before.CreatedAtUtc, user.CreatedAtUtc);
        Assert.Equal(foreign.User.Name, (await db.Users.SingleAsync(item => item.Id == foreign.User.Id)).Name);
        Assert.Equal(events, await db.SecurityActivities.CountAsync());
        var session = await db.UserSessions.SingleAsync(item => item.Id == originalSession.Id);
        Assert.Null(session.RevokedAtUtc); Assert.Equal(originalSession.ExpiresAtUtc, session.ExpiresAtUtc);
        using var profileRequest = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, "/api/auth/me", owner.AccessToken);
        using var profile = await client.SendAsync(profileRequest); Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        Assert.Equal("María del Mar", (await profile.Content.ReadFromJsonAsync<AccountUser>())!.Name);
        var version = user.ConcurrencyStamp;
        using var repeated = await RenameAsync(client, owner.AccessToken, new { name = "María del Mar" });
        Assert.Equal(HttpStatusCode.NoContent, repeated.StatusCode);
        Assert.Equal(version, (await db.Users.AsNoTracking().SingleAsync(item => item.Id == owner.User.Id)).ConcurrencyStamp);
        using var refreshed = await client.PostAsJsonAsync("/api/auth/refresh", new { owner.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Equal("María del Mar", (await refreshed.Content.ReadFromJsonAsync<AccountAuthentication>())!.User.Name);
    }

    [Fact]
    public async Task InvalidOrUnauthorizedNameChangesCannotMutateTheUserAndActingSessionIsRechecked()
    {
        using var factory = new AccountApiFactory(); using var client = factory.CreateApiClient();
        var owner = await factory.RegisterAsync(client, AccountCredentials.New());
        using var plain = await client.PostAsJsonAsync(Path, new { name = "Sin autorización" });
        Assert.Equal(HttpStatusCode.Unauthorized, plain.StatusCode);
        using var device = new HttpRequestMessage(HttpMethod.Post, Path);
        device.Headers.Authorization = new("Device", new string('A', 43)); device.Content = JsonContent.Create(new { name = "Sin autorización" });
        using var denied = await client.SendAsync(device); Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        foreach (var name in new[] { " ", "A", "Nombre\ncon salto", "Nombre\0oculto", new string('a', 101) })
        { using var invalid = await RenameAsync(client, owner.AccessToken, new { name }); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode); }
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var session = await db.UserSessions.SingleAsync(); session.RevokedAtUtc = factory.Clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<DeviceOperationException>(() => scope.ServiceProvider.GetRequiredService<AccountProfileService>()
            .UpdateNameAsync(owner.User.Id, session.Id, "Cambio inválido", CancellationToken.None));
        Assert.Equal(401, error.StatusCode);
        Assert.Equal(owner.User.Name, (await db.Users.SingleAsync()).Name);
    }
}
