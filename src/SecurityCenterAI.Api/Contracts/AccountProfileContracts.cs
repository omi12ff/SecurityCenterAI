using System.ComponentModel.DataAnnotations;

namespace SecurityCenterAI.Api.Contracts;

public sealed record UpdateProfileNameRequest([Required, MaxLength(200)] string Name);
