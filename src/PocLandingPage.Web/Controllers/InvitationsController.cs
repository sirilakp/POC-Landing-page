using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PocLandingPage.Web.Options;
using PocLandingPage.Web.Services;

namespace PocLandingPage.Web.Controllers;

[Authorize(Policy = "AdminOnly")]
[Route("admin/invitations")]
public class InvitationsController : Controller
{
    private readonly IInvitationService _invitations;
    private readonly AzureAdOptions _ad;

    public InvitationsController(IInvitationService invitations, IOptions<AzureAdOptions> ad)
    {
        _invitations = invitations;
        _ad = ad.Value;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.ServicePrincipalId = _ad.ServicePrincipalId;
        return View(await _invitations.GetGuestsAsync(ct));
    }
}
