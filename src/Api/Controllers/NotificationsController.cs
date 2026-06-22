using Application.DTOs;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class NotificationsController(INotificationService notificationService, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await notificationService.GetAllAsync(currentUser.UserId, cancellationToken));
    }

    [HttpPost("mark-read/{id:int}")]
    public async Task<IActionResult> MarkRead(int id, CancellationToken cancellationToken)
    {
        return await notificationService.MarkReadAsync(currentUser.UserId, id, cancellationToken) ? NoContent() : NotFound();
    }
}
