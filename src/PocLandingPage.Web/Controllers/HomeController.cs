using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PocLandingPage.Web.Models;
using PocLandingPage.Web.Services;

namespace PocLandingPage.Web.Controllers;

[Authorize(Policy = "ViewerOrAdmin")]
public class HomeController : Controller
{
    private readonly IPocService _pocs;

    public HomeController(IPocService pocs) => _pocs = pocs;

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var isAdmin = User.IsInRole(Roles.Admin);
        if (isAdmin)
            return View(await _pocs.GetAllAsync(ct));

        var oid = User.FindFirstValue("oid") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(oid))
            return Forbid();

        return View(await _pocs.GetVisibleForUserAsync(oid, ct));
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
