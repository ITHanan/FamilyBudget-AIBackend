using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;
using System.Text.RegularExpressions;

namespace Infrastructure.Services;

public sealed class AuthService(AppDbContext dbContext, IJwtTokenService jwtTokenService) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var username = request.Username.Trim().ToLowerInvariant();
        var firstName = request.FirstName.Trim();
        var lastName = request.LastName.Trim();
        var email = request.Email.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(username) || username.Length < 3)
        {
            throw new InvalidOperationException("Username is required and must be at least 3 characters.");
        }

        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
        {
            throw new InvalidOperationException("First name and last name are required.");
        }

        if (!IsValidEmail(email))
        {
            throw new InvalidOperationException("Enter a valid email address.");
        }

        var passwordErrors = ValidatePassword(request.Password, username, email);
        if (passwordErrors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(" ", passwordErrors));
        }

        var usernameExists = await dbContext.Users.AnyAsync(x => x.Username == username, cancellationToken);
        if (usernameExists)
        {
            throw new InvalidOperationException("A user with that username already exists.");
        }

        var emailExists = await dbContext.Users.AnyAsync(x => x.Email == email, cancellationToken);
        if (emailExists)
        {
            throw new InvalidOperationException("A user with that email already exists.");
        }

        var user = new User
        {
            Username = username,
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        var dto = ToDto(user);
        return jwtTokenService.CreateAuthResponse(dto);
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
        return jwtTokenService.CreateAuthResponse(dto);
    }

    public async Task<UserDto> GetMeAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FindAsync([userId], cancellationToken)
            ?? throw new UnauthorizedAccessException("User not found.");

        return ToDto(user);
    }

    private static UserDto ToDto(User user) => new(user.Id, user.Username, user.FirstName, user.LastName, user.Email, user.CreatedAt);

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 256)
        {
            return false;
        }

        try
        {
            var address = new MailAddress(email);
            return string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static IReadOnlyList<string> ValidatePassword(string password, string username, string email)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
        {
            errors.Add("Password must be at least 12 characters.");
        }

        if (!Regex.IsMatch(password, "[a-z]"))
        {
            errors.Add("Password must include a lowercase letter.");
        }

        if (!Regex.IsMatch(password, "[A-Z]"))
        {
            errors.Add("Password must include an uppercase letter.");
        }

        if (!Regex.IsMatch(password, "[0-9]"))
        {
            errors.Add("Password must include a number.");
        }

        if (!Regex.IsMatch(password, "[^a-zA-Z0-9]"))
        {
            errors.Add("Password must include a symbol.");
        }

        if (!string.IsNullOrWhiteSpace(username) &&
            password.Contains(username, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Password cannot contain your username.");
        }

        var emailName = email.Split('@')[0];
        if (!string.IsNullOrWhiteSpace(emailName) &&
            password.Contains(emailName, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Password cannot contain your email name.");
        }

        return errors;
    }
}
