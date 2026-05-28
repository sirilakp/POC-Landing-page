using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PocLandingPage.Web.Models;
using PocLandingPage.Web.Services;

namespace PocLandingPage.Web.Controllers;

[Authorize(Policy = "AdminOnly")]
[Route("admin/pocs")]
public class PocsAdminController : Controller
{
    private readonly IPocService _pocs;
    private readonly IInvitationService _invitations;
    private readonly IUserDirectoryService _users;
    private readonly IDescriptionGenerator _generator;

    public PocsAdminController(
        IPocService pocs,
        IInvitationService invitations,
        IUserDirectoryService users,
        IDescriptionGenerator generator)
    {
        _pocs = pocs;
        _invitations = invitations;
        _users = users;
        _generator = generator;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var pocs = await _pocs.GetAllAsync(ct);
        var allOids = pocs.SelectMany(p => p.AllowedUserIds).Distinct().ToList();
        var users = await _users.GetUsersByOidAsync(allOids, ct);

        ViewBag.Users = users;
        ViewBag.AiEnabled = _generator.IsEnabled;
        ViewBag.AvailableGuests = await _invitations.GetGuestsAsync(ct);

        return View(pocs);
    }
}
