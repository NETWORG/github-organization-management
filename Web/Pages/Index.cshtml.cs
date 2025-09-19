using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Identity.Web;
using System.Security.Claims;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Web.Pages
{
    [Authorize(AuthenticationSchemes = OpenIdConnectDefaults.AuthenticationScheme)]
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;

        public string? GitHubName { get; set; }
        public string? GitHubId { get; set; }
        public bool IsGitHubLinked => !string.IsNullOrEmpty(GitHubName);

        private readonly GraphServiceClient _microsoftGraph;
        private readonly HttpClient _httpClient;

        public IndexModel(ILogger<IndexModel> logger, GraphServiceClient microsoftGraph, HttpClient httpClient)
        {
            _logger = logger;
            _microsoftGraph = microsoftGraph;
            _httpClient = httpClient;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            this.GitHubId = await CheckGitHubLinkAsync();
            if (!string.IsNullOrEmpty(GitHubId))
            {
                var ghHelperService = new Services.GitHubHelperService(_httpClient);
                GitHubName = await ghHelperService.GetGitHubUsernameAsync(long.Parse(GitHubId));
            }

            // If not linked, check if the user is authenticated with GitHub (GitHub cookie)
            if (string.IsNullOrEmpty(GitHubName))
            {
                var githubResult = await HttpContext.AuthenticateAsync("GitHub");
                if (githubResult.Succeeded && githubResult.Principal != null)
                {
                    GitHubName = githubResult.Principal.FindFirst("urn:github:login")?.Value ?? githubResult.Principal.Identity?.Name;

                    // If redirected after linking, run the mock link logic
                    if (Request.Query.ContainsKey("link") && Request.Query["link"] == "True")
                    {
                        await LinkGitHubAsync(long.Parse(githubResult.Principal.GetNameIdentifierId()));
                    }
                }
            }
            return Page();
        }

        public async Task<IActionResult> OnPostLinkGitHubAsync()
        {
            // Redirect to GitHub OAuth, return to /Index?link=true
            return Challenge(new AuthenticationProperties { RedirectUri = Url.Page("/Index", null, new { link = true }) }, "GitHubOAuth");
        }

        public async Task<IActionResult> OnPostRemoveGitHubLinkAsync()
        {
            await _microsoftGraph.Users[User.FindFirst(ClaimConstants.ObjectId)?.Value]
                .PatchAsync(new User
                {
                    AdditionalData = new Dictionary<string, object>
                    {
                        { Helpers.Constants.ExtensionAttributeName, null }
                    }
                });
            await HttpContext.SignOutAsync("GitHub");
            return RedirectToPage();
        }

        private async Task LinkGitHubAsync(long userId)
        {
            await _microsoftGraph.Users[User.FindFirst(ClaimConstants.ObjectId)?.Value]
                .PatchAsync(new User
                {
                    AdditionalData = new Dictionary<string, object>
                    {
                        { Helpers.Constants.ExtensionAttributeName, userId.ToString() }
                    }
                });
        }

        private async Task<string?> CheckGitHubLinkAsync()
        {
            var user = await _microsoftGraph.Users[User.FindFirst(ClaimConstants.ObjectId)?.Value].GetAsync(x => x.QueryParameters.Select = [Helpers.Constants.ExtensionAttributeName]);
            return user.AdditionalData.ContainsKey(Helpers.Constants.ExtensionAttributeName) ? user.AdditionalData[Helpers.Constants.ExtensionAttributeName].ToString() : null;
        }
    }
}
