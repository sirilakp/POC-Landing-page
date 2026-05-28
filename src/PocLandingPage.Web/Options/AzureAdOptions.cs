using System.ComponentModel.DataAnnotations;

namespace PocLandingPage.Web.Options;

public class AzureAdOptions
{
    public const string SectionName = "AzureAd";

    [Required]
    public string Instance { get; set; } = "https://login.microsoftonline.com/";

    [Required]
    public string TenantId { get; set; } = "";

    [Required]
    public string ClientId { get; set; } = "";

    public string? ClientSecret { get; set; }

    public string CallbackPath { get; set; } = "/signin-oidc";

    [Required(ErrorMessage =
        "AzureAd:ServicePrincipalId is the Enterprise Application Object ID — see docs/manual-setup.md Phase 1.5.")]
    public string ServicePrincipalId { get; set; } = "";
}
