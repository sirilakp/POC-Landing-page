using System.ComponentModel.DataAnnotations;

namespace PocLandingPage.Web.Models;

public class PocEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, StringLength(120)]
    public string Name { get; set; } = "";

    [Required, Url, StringLength(2048)]
    public string Url { get; set; } = "";

    [StringLength(2000)]
    public string Description { get; set; } = "";

    public bool AllowAllViewers { get; set; }

    public List<string> AllowedUserIds { get; set; } = new();

    public bool IsVisibleTo(string userOid) =>
        AllowAllViewers ||
        AllowedUserIds.Contains(userOid, StringComparer.OrdinalIgnoreCase);
}
