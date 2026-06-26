using Application.DTOs;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/ai")]
public sealed class AIController(IAIConversationService aiService, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("conversations")]
    public async Task<ActionResult<IReadOnlyList<ConversationListDto>>> GetConversations(CancellationToken cancellationToken)
    {
        return Ok(await aiService.GetConversationsAsync(currentUser.UserId, cancellationToken));
    }

    [HttpPost("conversations")]
    public async Task<ActionResult<ConversationDto>> CreateConversation(CreateConversationRequest request, CancellationToken cancellationToken)
    {
        var conversation = await aiService.CreateConversationAsync(currentUser.UserId, request, cancellationToken);
        return CreatedAtAction(nameof(GetConversation), new { id = conversation.Id }, conversation);
    }

    [HttpGet("conversations/{id:int}")]
    public async Task<ActionResult<ConversationDto>> GetConversation(int id, CancellationToken cancellationToken)
    {
        var conversation = await aiService.GetConversationAsync(currentUser.UserId, id, cancellationToken);
        return conversation is null ? NotFound() : Ok(conversation);
    }

    [HttpPost("conversations/{id:int}/messages")]
    public async Task<ActionResult<SendMessageResponse>> SendMessage(int id, SendMessageRequest request, CancellationToken cancellationToken)
    {
        var response = await aiService.SendMessageAsync(currentUser.UserId, id, request, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpDelete("conversations/{id:int}")]
    public async Task<IActionResult> DeleteConversation(int id, CancellationToken cancellationToken)
    {
        return await aiService.DeleteConversationAsync(currentUser.UserId, id, cancellationToken) ? NoContent() : NotFound();
    }
}
