using System.Text.Json.Nodes;

namespace Web.Services
{
    public class GitHubHelperService
    {
        private readonly HttpClient _httpClient;
        public GitHubHelperService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string?> GetGitHubUsernameAsync(long githubId)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/user/{githubId}");
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.UserAgent.ParseAdd(Helpers.Constants.UserAgent);
            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var githubUser = await response.Content.ReadFromJsonAsync<JsonObject>();
                return githubUser?["login"]?.ToString();
            }
            throw new Exception($"Failed to get GitHub user info for ID {githubId}. Status code: {response.StatusCode}");
        }
    }
}
