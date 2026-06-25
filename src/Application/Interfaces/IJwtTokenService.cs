using Application.DTOs;

namespace Application.Interfaces;

public interface IJwtTokenService
{
    AuthResponse CreateAuthResponse(UserDto user);
}
