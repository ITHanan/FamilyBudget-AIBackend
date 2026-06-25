using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Application.DTOs;
using Application.Interfaces;
using Microsoft.IdentityModel.Tokens;

namespace Api.Services;

public sealed class JwtTokenService(IConfiguration configuration) : IJwtTokenService
{
    public AuthResponse CreateAuthResponse(UserDto user)
    {
        var key = configuration["Jwt:Key"] ?? "development-only-secret-key-change-me";
        var expiresAt = DateTime.UtcNow.AddMinutes(GetAccessTokenMinutes());
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim("first_name", user.FirstName),
            new Claim("last_name", user.LastName)
        };

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return new AuthResponse(new JwtSecurityTokenHandler().WriteToken(token), expiresAt, user);
    }

    private int GetAccessTokenMinutes()
    {
        var configuredMinutes = configuration.GetValue<int?>("Jwt:AccessTokenMinutes");
        return configuredMinutes is > 0 and <= 1440 ? configuredMinutes.Value : 60;
    }
}
