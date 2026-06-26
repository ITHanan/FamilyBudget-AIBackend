
using System.ComponentModel.DataAnnotations;

namespace Application.DTOs;

public sealed record RegisterRequest(
    [Required, MinLength(3), MaxLength(64)] string Username,
    [Required, MaxLength(100)] string FirstName,
    [Required, MaxLength(100)] string LastName,
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MinLength(12), MaxLength(128)] string Password);

public sealed record LoginRequest(
    [Required, MinLength(3), MaxLength(64)] string Username,
    [Required] string Password);

public sealed record UserDto(int Id, string Username, string FirstName, string LastName, string Email, DateTime CreatedAt);
public sealed record AuthResponse(string Token, DateTime ExpiresAt, UserDto User);
