using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Api.Services;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Tests;

public sealed class LinkMessageTests
{
    internal const string Path = "/api/tools/link-message";
    internal static Task<HttpResponseMessage> CheckAsync(HttpClient client, string token, string content = "https://example.invalid/", string kind = "link", bool confirmed = true)
    {
        var request = AccountApiFactory.AuthorizedRequest(HttpMethod.Post, Path, token);
        request.Content = JsonContent.Create(new { kind, content, confirmed });
        return client.SendAsync(request);
    }
    internal static async Task<LinkMessageQuota> QuotaAsync(HttpClient client, string token)
    {
        using var request = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, Path, token);
        using var response = await client.SendAsync(request); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        return (await response.Content.ReadFromJsonAsync<LinkMessageQuota>())!;
    }

    [Theory]
    [InlineData("http://example.invalid/", "unencrypted-link")]
    [InlineData("https://trusted.invalid:PRIVATE_FIXTURE@destination.invalid/?secret=PRIVATE_FIXTURE", "embedded-userinfo")]
    [InlineData("https://127.0.0.1/", "numeric-host")]
    [InlineData("https://xn--bcher-kva.example/", "internationalized-host")]
    [InlineData("https://example.invalid:8443/", "unusual-port")]
    [InlineData("https://tinyurl.com/fixture", "shortened-link")]
    public void LinkRulesDescribeStructureWithoutEchoingPrivatePathsOrClaimingFraud(string content, string code)
    {
        var report = LocalScamRules.Check(new("link", content, true));
        Assert.Contains(report.Signals, signal => signal.Code == code); Assert.Equal("signals-found", report.Outcome);
        Assert.DoesNotContain("PRIVATE_FIXTURE", JsonSerializer.Serialize(report));
        Assert.DoesNotContain(report.Hosts, host => host.Contains('/') || host.Contains('@') || host.Contains('?'));
        var unremarkable = LocalScamRules.Check(new("link", "https://tinyurl.com.example.invalid/", true));
        Assert.Equal("no-obvious-signals", unremarkable.Outcome);
    }

    [Fact]
    public void MessageRulesRespectWordBoundariesAccentsAndTheFiveLinkLimit()
    {
        var plain = LocalScamRules.Check(new("message", "Mi opinión: nos vemos el miércoles.", true));
        Assert.Empty(plain.Signals); Assert.Equal(0, plain.LinksChecked);
        var text = "¡URGENTE! Ganaste un premio. Ingresá tu contraseña y código de verificación para una transferencia. " +
            string.Join(" ", Enumerable.Range(1, 7).Select(index => $"http://fixture{index}.invalid/path?value=PRIVATE_FIXTURE"));
        var report = LocalScamRules.Check(new("message", text, true));
        Assert.Equal(5, report.LinksChecked); Assert.Equal(2, report.LinksOmitted);
        foreach (var code in new[] { "urgent-language", "credential-reference", "prize-reference", "payment-reference", "unencrypted-link" })
            Assert.Single(report.Signals, item => item.Code == code);
        Assert.DoesNotContain("PRIVATE_FIXTURE", JsonSerializer.Serialize(report));
        foreach (var content in new[] { "javascript:alert(1)", "file:///C:/private", "https://example.invalid/\nhttps://other.invalid", "https://example.invalid/" + new string('x', 2048) })
            Assert.Throws<DeviceOperationException>(() => LocalScamRules.Check(new("link", content, true)));
    }

    [Fact]
    public async Task AuthVerificationAndConsentGateChecksWithoutConsumingQuotaOnErrors()
    {
        using var factory = new AccountApiFactory(); using var client = factory.CreateApiClient();
        using (var anonymous = await client.GetAsync(Path)) Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        var pending = await factory.RegisterAsync(client, AccountCredentials.New());
        using (var response = await CheckAsync(client, pending.AccessToken)) Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var owner = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, AccountCredentials.New());
        foreach (var request in new[] { new LinkMessageRequest("link", "https://example.invalid", false), new("file", "PRIVATE_FIXTURE", true),
            new("link", "PRIVATE_FIXTURE", true), new("message", new string('x', 6001), true) })
        {
            using var response = await CheckAsync(client, owner.AccessToken, request.Content, request.Kind, request.Confirmed);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); Assert.True(response.Headers.CacheControl!.NoStore);
            Assert.DoesNotContain("PRIVATE_FIXTURE", await response.Content.ReadAsStringAsync());
        }
        Assert.Equal(0, (await QuotaAsync(client, owner.AccessToken)).Used);
        var device = await DeviceApiTestSupport.CreateAsync(client, owner.AccessToken);
        var paired = await DeviceApiTestSupport.PairAsync(client, device.PairingCode);
        using var deviceRequest = new HttpRequestMessage(HttpMethod.Get, Path); deviceRequest.Headers.Authorization = new("Device", paired.DeviceToken);
        using var denied = await client.SendAsync(deviceRequest); Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0); listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var local = await CheckAsync(client, owner.AccessToken, $"http://127.0.0.1:{port}/PRIVATE_FIXTURE");
            Assert.Equal(HttpStatusCode.OK, local.StatusCode); Assert.False(listener.Pending());
        }
        finally { listener.Stop(); }
    }

    [Fact]
    public async Task DailyQuotaSurvivesNewSessionsIsPrivateAndResetsOnTheNextUtcDay()
    {
        using var factory = new AccountApiFactory(linkMessageDailyLimit: 2); using var client = factory.CreateApiClient();
        var account = AccountCredentials.New(); var owner = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, account);
        var foreign = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, AccountCredentials.New());
        for (var index = 1; index <= 2; index++)
        {
            using var response = await CheckAsync(client, owner.AccessToken); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = (await response.Content.ReadFromJsonAsync<LinkMessageResponse>())!;
            Assert.Equal(index, result.Quota.Used); Assert.Equal(2 - index, result.Quota.Remaining);
        }
        using (var limited = await CheckAsync(client, owner.AccessToken)) Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal(0, (await QuotaAsync(client, foreign.AccessToken)).Used);
        var login = await AccountSessionManagementTests.LoginAsync(client, account);
        Assert.Equal(2, (await QuotaAsync(client, login.AccessToken)).Used);
        factory.Clock.Advance(TimeSpan.FromDays(1));
        var next = await AccountSessionManagementTests.LoginAsync(client, account);
        Assert.Equal(0, (await QuotaAsync(client, next.AccessToken)).Used);
        using var fresh = await CheckAsync(client, next.AccessToken); Assert.Equal(HttpStatusCode.OK, fresh.StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.LinkMessageUsage.CountAsync(item => item.UserId == owner.User.Id));
        Assert.Equal(1, (await db.LinkMessageUsage.SingleAsync(item => item.UserId == owner.User.Id)).Checks);
    }

    [Fact]
    public async Task ExportContainsOnlyOwnUsageMetadataAndAccountDeletionCascadesIt()
    {
        using var factory = new AccountApiFactory(); using var client = factory.CreateApiClient();
        var account = AccountCredentials.New(); var owner = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, account);
        var foreign = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, AccountCredentials.New());
        using (var check = await CheckAsync(client, owner.AccessToken, "Mensaje PRIVATE_FIXTURE urgente", "message")) Assert.Equal(HttpStatusCode.OK, check.StatusCode);
        using var exportRequest = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, "/api/account/export", owner.AccessToken);
        using var response = await client.SendAsync(exportRequest); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain("PRIVATE_FIXTURE", json);
        var export = JsonSerializer.Deserialize<AccountExportResponse>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(1, export.LinkMessageUsage!.Checks);
        using (var other = await CheckAsync(client, foreign.AccessToken)) Assert.Equal(HttpStatusCode.OK, other.StatusCode);
        using var deletion = AccountApiFactory.AuthorizedRequest(HttpMethod.Delete, "/api/account", owner.AccessToken);
        deletion.Content = JsonContent.Create(new { account.Password, confirmation = "ELIMINAR" });
        using var deleted = await client.SendAsync(deletion); Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.LinkMessageUsage.AnyAsync(item => item.UserId == owner.User.Id));
        Assert.True(await db.LinkMessageUsage.AnyAsync(item => item.UserId == foreign.User.Id));
    }
}
