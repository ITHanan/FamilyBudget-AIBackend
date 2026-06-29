using System.ComponentModel.DataAnnotations;

namespace Application.DTOs;

public sealed record CreateUserSuggestionRequest(
    [Required, MinLength(5), MaxLength(2000)]
    string Message);

public sealed record UserSuggestionDto(
    int Id,
    string Message,
    string RecipientEmail,
    DateTime CreatedAt);
