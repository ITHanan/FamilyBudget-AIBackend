using Application.DTOs;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/user-suggestions")]
public sealed class UserSuggestionsController(IUserSuggestionService userSuggestionService, ICurrentUserService currentUser) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<UserSuggestionDto>> Create(CreateUserSuggestionRequest request, CancellationToken cancellationToken)
    {
        var suggestion = await userSuggestionService.CreateAsync(currentUser.UserId, request, cancellationToken);
        return CreatedAtAction(nameof(Create), new { id = suggestion.Id }, suggestion);
    }
}
