using Azure.Identity;
using Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Graph;
using Microsoft.Identity.Abstractions;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.TokenCacheProviders.InMemory;
using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// TODO: This should probably be done via IOptions
Web.Helpers.Constants.ExtensionAttributeName = builder.Configuration["AzureAd:ExtensionAttributeName"];
Web.Helpers.Constants.ExemptUsers = builder.Configuration.GetSection("ExemptUsers").Get<string[]>();

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddMicrosoftIdentityWebApp(options =>
    {
        options.ClientId = builder.Configuration["AzureAd:ClientId"];
        options.TenantId = builder.Configuration["AzureAd:TenantId"];
        options.Instance = builder.Configuration["AzureAd:Instance"];
        options.ClientSecret = builder.Configuration["AzureAd:ClientSecret"];
        options.CallbackPath = "/signin-oidc";
    })
    .EnableTokenAcquisitionToCallDownstreamApi()
    .AddInMemoryTokenCaches();
builder.Services.AddAuthentication()
    .AddCookie("GitHub")
    .AddOAuth("GitHubOAuth", options =>
    {
        options.SignInScheme = "GitHub";
        options.ClientId = builder.Configuration["GitHub:ClientId"];
        options.ClientSecret = builder.Configuration["GitHub:ClientSecret"];
        options.CallbackPath = "/signin-github";
        options.AuthorizationEndpoint = "https://github.com/login/oauth/authorize";
        options.TokenEndpoint = "https://github.com/login/oauth/access_token";
        options.UserInformationEndpoint = "https://api.github.com/user";

        options.SaveTokens = true;

        options.Scope.Add("read:user");
        options.Scope.Add("user:email");

        options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
        options.ClaimActions.MapJsonKey(ClaimTypes.Name, "name");
        options.ClaimActions.MapJsonKey("urn:github:login", "login");

        options.Events = new OAuthEvents
        {
            OnCreatingTicket = async ctx =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, ctx.Options.UserInformationEndpoint);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.UserAgent.ParseAdd(Web.Helpers.Constants.UserAgent);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ctx.AccessToken);

                var response = await ctx.Backchannel.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ctx.HttpContext.RequestAborted);
                response.EnsureSuccessStatusCode();

                var user = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                ctx.RunClaimActions(user.RootElement);
            }
        };
    });

var appOnlyCredential = new ClientSecretCredential(
    builder.Configuration["AzureAd:TenantId"],
    builder.Configuration["AzureAd:ClientId"],
    builder.Configuration["AzureAd:ClientSecret"]);
builder.Services.AddSingleton(new GraphServiceClient(appOnlyCredential, new[] { "https://graph.microsoft.com/.default" }));

builder.Services.AddSingleton<MicrosoftGraphService>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();

app.Run();
