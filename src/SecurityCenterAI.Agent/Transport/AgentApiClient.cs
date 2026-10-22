using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SecurityCenterAI.Agent.Checks;
using SecurityCenterAI.Agent.Security;

namespace SecurityCenterAI.Agent.Transport;

public sealed class AgentApiClient
{
    private const int MaximumResponseBytes = 16 * 1024;
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);
    private readonly HttpClient http;
    private readonly Uri apiBaseUri;
    private readonly TimeProvider clock;

    public AgentApiClient(HttpClient http, Uri apiBaseUri, TimeProvider clock)
    {
        this.http = http;
        this.apiBaseUri = ApiEndpoint.Parse(apiBaseUri.AbsoluteUri);
        this.clock = clock;
    }

    public static HttpClient CreateHttpClient() => new(new HttpClientHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        // Local development must never send its HTTP pairing code through a system proxy.
        UseProxy = false
    }) { Timeout = TimeSpan.FromSeconds(15) };

    public async Task<DeviceCredential> PairAsync(string pairingCode, string agentVersion, CancellationToken cancellationToken)
    {
        if (!DeviceCredential.IsOpaqueSecret(pairingCode))
            throw new AgentException("El código debe tener 43 caracteres. Copialo completo desde Mis dispositivos.");
        ValidateVersion(agentVersion);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(apiBaseUri, "api/agent/pair"))
        {
            Content = JsonContent.Create(new { pairingCode, agentVersion })
        };
        using var response = await SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Created) throw ResponseError(response.StatusCode);
        try
        {
            var body = await ReadBoundedAsync(response.Content, cancellationToken, pairing: true);
            using var document = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 8 });
            var root = document.RootElement;
            var credential = new DeviceCredential(apiBaseUri, root.GetProperty("deviceId").GetGuid(),
                root.GetProperty("deviceToken").GetString()!, root.GetProperty("expiresAtUtc").GetDateTimeOffset());
            credential.ValidateExpiry(clock);
            return credential;
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or
            InvalidOperationException or FormatException or ArgumentException)
        {
            throw new AgentException("La API devolvió una vinculación inválida. Generá un código nuevo antes de reintentar.");
        }
    }

    public async Task HeartbeatAsync(DeviceCredential credential, string agentVersion, CancellationToken cancellationToken)
    {
        if (credential.ApiBaseUri != apiBaseUri) throw new AgentException("La credencial pertenece a otra API.");
        credential.ValidateExpiry(clock);
        ValidateVersion(agentVersion);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(apiBaseUri, "api/agent/heartbeat"))
        {
            Content = JsonContent.Create(new { agentVersion })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Device", credential.DeviceToken);
        using var response = await SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.NoContent) throw ResponseError(response.StatusCode);
    }

    public async Task<AnalysisReceipt> SubmitAnalysisAsync(DeviceCredential credential, AgentReport report,
        CancellationToken cancellationToken)
    {
        if (credential.ApiBaseUri != apiBaseUri) throw new AgentException("La credencial pertenece a otra API.");
        credential.ValidateExpiry(clock);
        var submission = ReportSubmission.Create(report);
        if (submission.StartedAtUtc < clock.GetUtcNow().AddDays(-7) || submission.CompletedAtUtc > clock.GetUtcNow().AddMinutes(5))
            throw new AgentException("El informe es demasiado antiguo o su fecha no es válida. Revisá el reloj y ejecutá scan de nuevo.");
        var body = JsonSerializer.SerializeToUtf8Bytes(submission, WebJson);
        if (body.Length > 65536) throw new AgentException("El informe supera el límite de envío de 64 KiB.");
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(apiBaseUri, "api/agent/analyses"))
        { Content = new ByteArrayContent(body) };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Headers.Authorization = new AuthenticationHeaderValue("Device", credential.DeviceToken);
        using var response = await SendAsync(request, cancellationToken);
        if (response.StatusCode is not (HttpStatusCode.Created or HttpStatusCode.OK))
            throw SubmissionError(response.StatusCode);
        try
        {
            var bytes = await ReadBoundedAsync(response.Content, cancellationToken);
            var receipt = JsonSerializer.Deserialize<AnalysisReceipt>(bytes, WebJson)
                ?? throw InvalidReceipt();
            var passed = submission.Checks.Count(item => item.Status == "passed");
            var attention = submission.Checks.Count(item => item.Status == "attention");
            var unknown = submission.Checks.Count(item => item.Status == "unknown");
            var na = submission.Checks.Count(item => item.Status == "notApplicable");
            var coverage = na == 10 ? 0 : 100 * (passed + attention) / (10 - na);
            if (receipt.AnalysisId == Guid.Empty || receipt.ReportId != report.ReportId || receipt.ScoringVersion != 1 ||
                receipt.CoveragePercent != coverage || receipt.PassedCount != passed || receipt.AttentionCount != attention ||
                receipt.UnknownCount != unknown || receipt.NotApplicableCount != na || receipt.Score is < 0 or > 100 ||
                ((passed + attention == 0) != (receipt.Score is null)) || receipt.ReceivedAtUtc.Year < 2000 ||
                receipt.AlreadyReceived != (response.StatusCode == HttpStatusCode.OK)) throw InvalidReceipt();
            return receipt;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        { throw InvalidReceipt(); }
    }

    private static AgentException InvalidReceipt() => new(
        "No se pudo validar la confirmación de la API. Reintentá con send; el mismo informe no se duplica.");

    private static AgentException SubmissionError(HttpStatusCode status) => status switch
    {
        HttpStatusCode.BadRequest => new("La API rechazó el formato o las fechas del informe. Revisá el reloj y generá uno nuevo con scan."),
        HttpStatusCode.Conflict => new("El identificador del informe ya tiene otro contenido o cambió el acceso. Generá un informe nuevo o revisá el equipo en la web."),
        HttpStatusCode.RequestEntityTooLarge => new("El informe supera el límite de envío de 64 KiB."),
        HttpStatusCode.NotFound => new("La API no tiene disponible la ingesta. Iniciá la versión actualizada del backend."),
        _ => ResponseError(status)
    };

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try { return await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new AgentException("La API tardó demasiado en responder. Comprobá su disponibilidad."); }
        catch (HttpRequestException)
        { throw new AgentException("No se pudo conectar con la API. Revisá que esté iniciada y que su certificado sea válido."); }
    }

    private static async Task<byte[]> ReadBoundedAsync(HttpContent content, CancellationToken cancellationToken, bool pairing = false)
    {
        if (content.Headers.ContentLength > MaximumResponseBytes) throw TooLarge();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            await using var stream = await content.ReadAsStreamAsync(deadline.Token);
            using var output = new MemoryStream();
            var buffer = new byte[4096];
            while (true)
            {
                var count = await stream.ReadAsync(buffer, deadline.Token);
                if (count == 0) return output.ToArray();
                if (output.Length + count > MaximumResponseBytes) throw TooLarge();
                output.Write(buffer, 0, count);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new AgentException(pairing ? "La API tardó demasiado en enviar la respuesta. Generá un código nuevo antes de reintentar la vinculación." : "La API tardó demasiado en enviar la confirmación. Reintentá con send; el mismo informe no se duplica."); }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        { throw new AgentException(pairing ? "La conexión se interrumpió al recibir la vinculación. Generá un código nuevo antes de reintentar." : "La conexión se interrumpió al recibir la confirmación. Reintentá con send; el mismo informe no se duplica."); }
    }

    private static AgentException TooLarge() => new("La API devolvió una respuesta mayor de la permitida.");

    private static void ValidateVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version) || version.Length > 64 || version.Any(char.IsControl))
            throw new AgentException("La versión del agente no tiene un formato válido.");
    }

    private static AgentException ResponseError(HttpStatusCode status) => status switch
    {
        HttpStatusCode.BadRequest => new("El código no es válido, venció o ya fue usado. Generá uno nuevo desde la web."),
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new("El acceso fue rechazado. La credencial pudo vencer o ser revocada; vinculá de nuevo."),
        HttpStatusCode.TooManyRequests => new("Se alcanzó el límite de solicitudes. Esperá un minuto antes de reintentar."),
        HttpStatusCode.Conflict => new("El estado del dispositivo cambió. Revisalo en la web antes de volver a vincular."),
        >= HttpStatusCode.MultipleChoices and < HttpStatusCode.BadRequest => new("La API intentó redirigir la solicitud. Revisá su URL; no se siguió la redirección."),
        _ => new("La API no pudo completar la operación. Revisá su disponibilidad antes de reintentar.")
    };
}
