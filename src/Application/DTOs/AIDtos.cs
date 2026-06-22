namespace Application.DTOs;

public sealed record CreateConversationRequest(string? Title);
public sealed record ConversationListDto(int Id, string Title, DateTime CreatedAt, DateTime UpdatedAt);
public sealed record MessageDto(int Id, string Role, string Content, DateTime CreatedAt);
public sealed record ConversationDto(int Id, string Title, DateTime CreatedAt, DateTime UpdatedAt, IReadOnlyList<MessageDto> Messages);
public sealed record SendMessageRequest(string Content);
public sealed record SendMessageResponse(MessageDto UserMessage, MessageDto AssistantMessage);
