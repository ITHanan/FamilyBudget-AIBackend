using Application.DTOs;

namespace Application.Interfaces;

public interface IJwtTokenService
{
    string CreateToken(UserDto user);
}
