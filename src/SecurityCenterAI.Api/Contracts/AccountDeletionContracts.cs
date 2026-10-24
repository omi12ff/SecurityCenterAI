using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SecurityCenterAI.Api.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DeleteAccountRequest(
    [Required, MaxLength(128)] string Password,
    [Required, MaxLength(8)] string Confirmation);
