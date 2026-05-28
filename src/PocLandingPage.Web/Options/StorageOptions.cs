using System.ComponentModel.DataAnnotations;

namespace PocLandingPage.Web.Options;

public class StorageOptions
{
    public const string SectionName = "Storage";

    [Required]
    public string BlobEndpoint { get; set; } = "";

    public string Container { get; set; } = "poc-data";

    public string BlobName { get; set; } = "pocs.json";
}
