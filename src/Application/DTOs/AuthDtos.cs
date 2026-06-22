namespace Application.DTOs;

public sealed record RegisterRequest(string Email, string Password);
public sealed record LoginRequest(string Email, string Password);
public sealed record UserDto(int Id, string Email, DateTime CreatedAt);
public sealed record AuthResponse(string Token, UserDto User);
