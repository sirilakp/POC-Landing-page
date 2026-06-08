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
            await _svc.InviteUserAsync(req.Email, ct);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (ODataError ex)
        {
            return BadRequest(ex.Error?.Message ?? "Microsoft Graph rejected the invitation.");
        }
        return Ok(new { message = $"Invitation sent to {req.Email}. Assign their role in the Azure Portal once they accept." });
    }
}
