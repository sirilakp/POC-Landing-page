using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Graph;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using PocLandingPage.Web.Models;
using PocLandingPage.Web.Options;
using PocLandingPage.Web.Services;

var builder = WebApplication.CreateBuilder(args);
var useLocalDevAuth = builder.Environment.IsDevelopment() &&
    builder.Configuration.GetValue("Auth:DisableEntraId", true);

if (useLocalDevAuth)
{
    builder.Services.AddOptions<AzureAdOptions>()
        .Bind(builder.Configuration.GetSection(AzureAdOptions.SectionName));
}
else
{
    builder.Services.AddOptions<AzureAdOptions>()
        .Bind(builder.Configuration.GetSection(AzureAdOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();
}

builder.Services.AddOptions<StorageOptions>()
    .Bind(builder.Configuration.GetSection(StorageOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<AzureOpenAIOptions>()
    .Bind(builder.Configuration.GetSection(AzureOpenAIOptions.SectionName));

if (useLocalDevAuth)
{
    builder.Services.AddAuthentication("LocalDevAuth")
        .AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>("LocalDevAuth", _ => { });
}
else
{
    builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection(AzureAdOptions.SectionName));
}

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ViewerOrAdmin", policy =>
        policy.RequireRole(Roles.Viewer, Roles.Admin));
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole(Roles.Admin));
});

builder.Services.AddControllersWithViews(options =>
{
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new AuthorizeFilter(policy));
});

if (useLocalDevAuth)
{
    builder.Services.AddRazorPages();
}
else
{
    builder.Services.AddRazorPages()
        .AddMicrosoftIdentityUI();
}

builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();

if (!useLocalDevAuth)
{
    builder.Services.AddApplicationInsightsTelemetry();
}

if (useLocalDevAuth)
{
    builder.Services.AddSingleton<IBlobStore, DevelopmentBlobStore>();
    builder.Services.AddScoped<IUserDirectoryService, DevelopmentUserDirectoryService>();
    builder.Services.AddScoped<IInvitationService, DevelopmentInvitationService>();
}
else
{
    // DefaultAzureCredential probes Managed Identity in prod and Azure CLI / Visual Studio creds in dev.
    TokenCredential credential = new DefaultAzureCredential();

    builder.Services.AddSingleton(sp =>
    {
        var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<StorageOptions>>().Value;
        return new BlobServiceClient(new Uri(opts.BlobEndpoint), credential);
    });

    builder.Services.AddSingleton<GraphServiceClient>(_ =>
        new GraphServiceClient(credential, new[] { "https://graph.microsoft.com/.default" }));

    builder.Services.AddSingleton(sp =>
    {
        var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AzureOpenAIOptions>>().Value;
        if (!opts.IsEnabled)
            return null!;
        return new AzureOpenAIClient(new Uri(opts.Endpoint!), credential);
    });

    builder.Services.AddScoped<IBlobStore, AzureBlobStore>();
    builder.Services.AddScoped<IGraphUserLookup, GraphUserLookup>();
    builder.Services.AddScoped<IUserDirectoryService, UserDirectoryService>();
    builder.Services.AddScoped<IInvitationService, InvitationService>();
}

builder.Services.AddScoped<IPocService, PocService>();
builder.Services.AddScoped<IDescriptionGenerator>(sp =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AzureOpenAIOptions>>().Value;
    if (!opts.IsEnabled) return new NullDescriptionGenerator();
    var client = sp.GetRequiredService<AzureOpenAIClient>();
    return new AzureOpenAIDescriptionGenerator(client, sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AzureOpenAIOptions>>());
});

var app = builder.Build();

if (!useLocalDevAuth)
{
    // Startup guard: validate AzureAd config eagerly so a missing ServicePrincipalId
    // fails on boot rather than at the first Graph call. See docs/manual-setup.md.
    var adValidate = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<AzureAdOptions>>();
    _ = adValidate.Value;
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();

public partial class Program { }
