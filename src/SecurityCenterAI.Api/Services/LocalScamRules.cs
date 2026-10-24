using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SecurityCenterAI.Api.Contracts;

namespace SecurityCenterAI.Api.Services;

public static class LocalScamRules
{
    public const string Version = "local-signals-v1";
    public const int MaxMessageLength = 6000;
    public const int MaxLinkLength = 2048;
    private static readonly Regex Links = Pattern("https?://[^\\s<>\"']+");
    private static Regex Pattern(string pattern) => new(pattern,
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(100));
    private static readonly (Regex Pattern, LinkMessageSignal Signal)[] MessagePatterns =
    [
        (Pattern(@"\b(urgente|ahora mismo|ultimo aviso|ultima advertencia|inmediatamente)\b"),
            new("urgent-language", "Lenguaje de urgencia", "El texto contiene expresiones que presionan para actuar rápido.", "Tomate tiempo y contrastá la solicitud por un canal oficial conocido.")),
        (Pattern(@"\b(contrasena|clave|pin|cvv|token|codigo (de (seguridad|verificacion)|sms))\b"),
            new("credential-reference", "Referencia a claves o códigos", "El texto menciona credenciales o códigos. La regla no distingue una solicitud de una advertencia educativa.", "Verificá qué se solicita. Evitá compartir claves o códigos por mensajes inesperados.")),
        (Pattern(@"\b(premio|ganaste|sorteo|recompensa)\b"),
            new("prize-reference", "Referencia a premios", "El texto menciona un premio o recompensa; puede ser legítimo y requiere contexto.", "Comprobá la promoción directamente con quien la organiza antes de entregar datos.")),
        (Pattern(@"\b(transferencia|deposito|pago adelantado|anticipo|criptomonedas?|bitcoin)\b"),
            new("payment-reference", "Referencia a pagos o transferencias", "El texto menciona pagos; esta observación no demuestra un fraude.", "Confirmá la identidad y la solicitud por un canal independiente antes de decidir."))
    ];

    public static LocalScamReport Check(LinkMessageRequest request)
    {
        if (!request.Confirmed || request.Kind is not ("link" or "message") || string.IsNullOrWhiteSpace(request.Content) ||
            request.Content.Length > (request.Kind == "link" ? MaxLinkLength : MaxMessageLength)) throw InvalidInput();
        var signals = new Dictionary<string, LinkMessageSignal>(StringComparer.Ordinal);
        var hosts = new HashSet<string>(StringComparer.Ordinal);
        var checkedLinks = 0; var omitted = 0;
        void Add(string code, string title, string explanation, string advice) => signals.TryAdd(code, new(code, title, explanation, advice));
        bool Inspect(string candidate)
        {
            if (candidate.Length > MaxLinkLength || candidate.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)) ||
                !Uri.TryCreate(candidate, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || string.IsNullOrEmpty(uri.Host)) return false;
            string host;
            try { host = uri.IdnHost.ToLowerInvariant().TrimEnd('.'); }
            catch (UriFormatException) { return false; }
            hosts.Add(host); checkedLinks++;
            if (uri.Scheme == "http") Add("unencrypted-link", "Enlace HTTP sin cifrado", "La dirección no usa HTTPS; esto no determina si el sitio es fraudulento.", "Buscá la dirección oficial con HTTPS antes de ingresar datos sensibles.");
            if (!string.IsNullOrEmpty(uri.UserInfo)) Add("embedded-userinfo", "Datos de usuario antes del dominio", "La dirección incluye una sección de usuario antes del signo @. Puede hacer confusa la identidad del destino.", "Revisá el dominio real mostrado; el texto anterior al @ no lo identifica.");
            if (uri.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6) Add("numeric-host", "Destino identificado por dirección IP", "El destino usa una IP, algo posible en servicios legítimos y que exige contexto.", "Confirmá con el remitente por un canal conocido a qué servicio pertenece.");
            if (host.Split('.').Any(label => label.StartsWith("xn--", StringComparison.Ordinal))) Add("internationalized-host", "Dominio internacionalizado", "El dominio usa representación punycode. No implica fraude, pero puede resultar difícil compararlo visualmente.", "Compará la forma ASCII mostrada con la dirección oficial que ya conocés.");
            if (!uri.IsDefaultPort) Add("unusual-port", "Puerto distinto del habitual", "La dirección especifica un puerto distinto del predeterminado del protocolo.", "Confirmá que corresponde al servicio esperado; puede ser una configuración legítima.");
            if (host is "tinyurl.com" or "t.co") Add("shortened-link", "Servicio de enlaces acortados", "La dirección pertenece a uno de dos servicios de acortamiento reconocidos por estas reglas. El destino final no fue consultado.", "Pedí la dirección oficial completa y contrastala por un canal conocido. Acortar un enlace no demuestra fraude.");
            return true;
        }
        if (request.Kind == "link")
        {
            if (!Inspect(request.Content.Trim())) throw InvalidInput();
        }
        else
        {
            string normalized;
            try { normalized = new string(request.Content.Normalize(NormalizationForm.FormD)
                .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark).ToArray()); }
            catch (ArgumentException) { throw InvalidInput(); }
            foreach (var (pattern, signal) in MessagePatterns) if (pattern.IsMatch(normalized)) signals.TryAdd(signal.Code, signal);
            var candidates = Links.Matches(request.Content);
            for (var index = 0; index < candidates.Count; index++)
            {
                if (index >= 5 || !Inspect(candidates[index].Value.TrimEnd('.', ',', ';', ')', ']', '}'))) omitted++;
            }
            if (request.Content.Any(character => CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.Format))
                Add("invisible-characters", "Caracteres de formato invisibles", "El texto contiene caracteres de formato que pueden tener usos legítimos o dificultar la lectura.", "Contrastá el mensaje y sus direcciones por otro canal.");
        }
        return new(Version, request.Kind, signals.Count == 0 ? "no-obvious-signals" : "signals-found",
            signals.Values.ToArray(), hosts.Order(StringComparer.Ordinal).ToArray(), checkedLinks, omitted);
    }

    private static DeviceOperationException InvalidInput() => new(400,
        "Elegí enlace HTTP/HTTPS o mensaje, respetá el límite de longitud y confirmá el envío. No incluyas claves o datos privados.");
}
