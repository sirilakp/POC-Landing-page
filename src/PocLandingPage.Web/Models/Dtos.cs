using System.ComponentModel.DataAnnotations;

namespace PocLandingPage.Web.Models;

public static class Roles
{
    public const string Admin = "POC.Admin";
    public const string Viewer = "POC.Viewer";

    public static bool IsValid(string? role) => role is Admin or Viewer;
}

public class InviteUserRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required]
    public string Role { get; set; } = Roles.Viewer;
}

public class UpdateRoleRequest
{
    [Required]
    public string Role { get; set; } = Roles.Viewer;
}

public class UpsertPocRequest
{
    [Required, StringLength(120)]
    public string Name { get; set; } = "";

    [Required, Url, StringLength(2048)]
    public string Url { get; set; } = "";

    [StringLength(2000)]
    public string Description { get; set; } = "";

    public bool AllowAllViewers { get; set; }

    public List<string> AllowedEmails { get; set; } = new();
}

public class SetAccessRequest
{
    public bool AllowAllViewers { get; set; }
    public List<string> AllowedEmails { get; set; } = new();
}

public class ReorderPocsRequest
{
    [Required]
    public List<Guid> OrderedIds { get; set; } = new();
}

public class AccessView
{
    public bool AllowAllViewers { get; set; }
    public List<UserSummary> Users { get; set; } = new();
}

public class UserSummary
{
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
}

public class GenerateDescriptionRequest
{
    [Required, StringLength(120)]
    public string Name { get; set; } = "";

    [Url, StringLength(2048)]
    public string? Url { get; set; }

    [StringLength(500)]
    public string? Keywords { get; set; }
}

public class UpsertPocResult
{
    public PocEntry Poc { get; set; } = default!;
    public List<string> UnresolvedEmails { get; set; } = new();
}
