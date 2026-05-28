namespace PocLandingPage.Web.Models;

public class GuestUser
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Role { get; set; }
    public string? InviteStatus { get; set; }
}
