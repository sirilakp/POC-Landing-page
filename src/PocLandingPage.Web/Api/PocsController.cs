using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PocLandingPage.Web.Models;
using PocLandingPage.Web.Services;

namespace PocLandingPage.Web.Api;

[ApiController]
[Route("api/pocs")]
[Authorize(Policy = "ViewerOrAdmin")]
public class PocsController : ControllerBase
{
    private readonly IPocService _pocs;
    private readonly IUserDirectoryService _users;
    private readonly IDescriptionGenerator _generator;

    public PocsController(IPocService pocs, IUserDirectoryService users, IDescriptionGenerator generator)
    {
        _pocs = pocs;
        _users = users;
        _generator = generator;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        if (User.IsInRole(Roles.Admin))
            return Ok(await _pocs.GetAllAsync(ct));

        var oid = User.FindFirstValue("oid") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(oid)) return Forbid();

        return Ok(await _pocs.GetVisibleForUserAsync(oid, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var entry = await _pocs.GetByIdAsync(id, ct);
        if (entry is null) return NotFound();

        if (User.IsInRole(Roles.Admin)) return Ok(entry);

        var oid = User.FindFirstValue("oid") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(oid) || !entry.IsVisibleTo(oid)) return Forbid();
        return Ok(entry);
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Create(UpsertPocRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var resolved = await _users.ResolveEmailsAsync(req.AllowedEmails, ct);
        var entry = new PocEntry
        {
            Name = req.Name,
            Url = req.Url,
            Description = req.Description,
            AllowAllViewers = req.AllowAllViewers,
            AllowedUserIds = resolved.Oids,
        };
        var saved = await _pocs.AddAsync(entry, ct);
        return Created($"/api/pocs/{saved.Id}", new UpsertPocResult { Poc = saved, UnresolvedEmails = resolved.Unresolved });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Update(Guid id, UpsertPocRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var resolved = await _users.ResolveEmailsAsync(req.AllowedEmails, ct);
        var updated = await _pocs.UpdateAsync(id, p =>
        {
            p.Name = req.Name;
            p.Url = req.Url;
            p.Description = req.Description;
            p.AllowAllViewers = req.AllowAllViewers;
            p.AllowedUserIds = resolved.Oids;
        }, ct);

        if (updated is null) return NotFound();
        return Ok(new UpsertPocResult { Poc = updated, UnresolvedEmails = resolved.Unresolved });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        await _pocs.DeleteAsync(id, ct) ? NoContent() : NotFound();

    [HttpGet("{id:guid}/access")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> GetAccess(Guid id, CancellationToken ct)
    {
        var entry = await _pocs.GetByIdAsync(id, ct);
        if (entry is null) return NotFound();

        var users = await _users.GetUsersByOidAsync(entry.AllowedUserIds, ct);
        return Ok(new AccessView
        {
            AllowAllViewers = entry.AllowAllViewers,
            Users = users.Values.ToList(),
        });
    }

    [HttpPut("{id:guid}/access")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> SetAccess(Guid id, SetAccessRequest req, CancellationToken ct)
    {
        var resolved = await _users.ResolveEmailsAsync(req.AllowedEmails, ct);
        var updated = await _pocs.UpdateAsync(id, p =>
        {
            p.AllowAllViewers = req.AllowAllViewers;
            p.AllowedUserIds = resolved.Oids;
        }, ct);
        if (updated is null) return NotFound();
        return Ok(new { unresolvedEmails = resolved.Unresolved });
    }

    [HttpPost("generate-description")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> GenerateDescription(GenerateDescriptionRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        if (!_generator.IsEnabled)
            return Problem("AI description generation is not configured.", statusCode: 503);

        var description = await _generator.GenerateAsync(req.Name, req.Url, req.Keywords, ct);
        return Ok(new { description });
    }
}
