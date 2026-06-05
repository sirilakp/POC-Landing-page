using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph.Models.ODataErrors;
using PocLandingPage.Web.Models;
using PocLandingPage.Web.Services;

namespace PocLandingPage.Web.Api;

[ApiController]
[Route("api/invitations")]
[Authorize(Policy = "AdminOnly")]
public class InvitationsApiController : ControllerBase
{
    private readonly IInvitationService _svc;

    public InvitationsApiController(IInvitationService svc) => _svc = svc;

    [HttpGet("guests")]
    public async Task<IActionResult> GetGuests(CancellationToken ct) =>
        Ok(await _svc.GetGuestsAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Invite(InviteUserRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            await _svc.InviteUserAsync(req.Email, req.Role, ct);
        }
        catch (InvalidOperationException ex)
        {
            // Domain-level guard (e.g. internal user, missing role) — show the message as-is.
            return BadRequest(ex.Message);
        }
        catch (ODataError ex)
        {
            // Surface the real Graph failure instead of a generic 500 HTML page.
            return BadRequest(ex.Error?.Message ?? "Microsoft Graph rejected the invitation.");
        }
        return Ok(new { message = $"Invitation sent to {req.Email}" });
    }

    [HttpPut("{userId}/role")]
    public async Task<IActionResult> UpdateRole(string userId, [FromBody] UpdateRoleRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        await _svc.UpdateRoleAsync(userId, req.Role, ct);
        return NoContent();
    }

    [HttpDelete("{userId}")]
    public async Task<IActionResult> Revoke(string userId, CancellationToken ct)
    {
        await _svc.RevokeAccessAsync(userId, ct);
        return NoContent();
    }
}
