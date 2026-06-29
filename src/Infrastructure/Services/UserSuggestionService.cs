using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Services;

public sealed class UserSuggestionService(AppDbContext dbContext, IConfiguration configuration) : IUserSuggestionService
{
    private const string DefaultRecipientEmail = "ithanan@gmail.com";

    public async Task<UserSuggestionDto> CreateAsync(int userId, CreateUserSuggestionRequest request, CancellationToken cancellationToken)
    {
        var message = request.Message.Trim();
        if (message.Length < 5)
        {
            throw new InvalidOperationException("Suggestion must be at least 5 characters.");
        }

        var suggestion = new UserSuggestion
        {
            UserId = userId,
            Message = message,
            RecipientEmail = configuration["ProductFeedback:RecipientEmail"] ?? DefaultRecipientEmail
        };

        dbContext.UserSuggestions.Add(suggestion);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new UserSuggestionDto(suggestion.Id, suggestion.Message, suggestion.RecipientEmail, suggestion.CreatedAt);
    }
}
