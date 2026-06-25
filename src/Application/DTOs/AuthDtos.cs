
namespace Application.DTOs;

public sealed record RegisterRequest(string Username, string FirstName, string LastName, string Email, string Password);
public sealed record LoginRequest(string Username, string Password);
public sealed record UserDto(int Id, string Username, string FirstName, string LastName, string Email, DateTime CreatedAt);
public sealed record AuthResponse(string Token, UserDto User);
