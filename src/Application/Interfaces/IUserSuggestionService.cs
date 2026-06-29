using Application.DTOs;

namespace Application.Interfaces;

public interface IUserSuggestionService
{
    Task<UserSuggestionDto> CreateAsync(int userId, CreateUserSuggestionRequest request, CancellationToken cancellationToken);
}
