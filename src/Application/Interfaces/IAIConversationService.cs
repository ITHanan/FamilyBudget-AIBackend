using Application.DTOs;

namespace Application.Interfaces;

public interface IAIConversationService
{
    Task<IReadOnlyList<ConversationListDto>> GetConversationsAsync(int userId, CancellationToken cancellationToken);
    Task<ConversationDto> CreateConversationAsync(int userId, CreateConversationRequest request, CancellationToken cancellationToken);
    Task<ConversationDto?> GetConversationAsync(int userId, int id, CancellationToken cancellationToken);
    Task<SendMessageResponse?> SendMessageAsync(int userId, int conversationId, SendMessageRequest request, CancellationToken cancellationToken);
    Task<bool> DeleteConversationAsync(int userId, int id, CancellationToken cancellationToken);
}
