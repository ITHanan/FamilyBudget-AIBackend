using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class AuthService(AppDbContext dbContext, IJwtTokenService jwtTokenService) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var username = request.Username.Trim().ToLowerInvariant();
        var firstName = request.FirstName.Trim();
        var lastName = request.LastName.Trim();

        if (string.IsNullOrWhiteSpace(username) || username.Length < 3)
        {
            throw new InvalidOperationException("Username is required and must be at least 3 characters.");
        }

        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
        {
            throw new InvalidOperationException("First name and last name are required.");
        }

        if (request.Password.Length < 8)
        {
            throw new InvalidOperationException("Password must be at least 8 characters.");
        }

        var usernameExists = await dbContext.Users.AnyAsync(x => x.Username == username, cancellationToken);
        if (usernameExists)
        {
            throw new InvalidOperationException("A user with that username already exists.");
        }

        var user = new User
        {
            Username = username,
            FirstName = firstName,
            LastName = lastName,
            Email = $"{username}@familybudget.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        var dto = ToDto(user);
        return new AuthResponse(jwtTokenService.CreateToken(dto), dto);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var username = request.Username.Trim().ToLowerInvariant();
        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Username == username, cancellationToken);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid username or password.");
        }

        var dto = ToDto(user);
        return new AuthResponse(jwtTokenService.CreateToken(dto), dto);
    }

    public async Task<UserDto> GetMeAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FindAsync([userId], cancellationToken)
            ?? throw new UnauthorizedAccessException("User not found.");

        return ToDto(user);
    }

    private static UserDto ToDto(User user) => new(user.Id, user.Username, user.FirstName, user.LastName, user.Email, user.CreatedAt);
}
