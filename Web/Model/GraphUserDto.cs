using Microsoft.Graph.Models;

namespace Web.Model
{
    public class GraphUserDto
    {
        public string Id { get; set; }
        public long? GitHubId { get; set; }
        public string Upn { get; set; }

        public static GraphUserDto From(User user)
        {
            var dto = new GraphUserDto
            {
                Id = user.Id,
                Upn = user.UserPrincipalName,
                GitHubId = null
            };
            if (user.AdditionalData != null && user.AdditionalData.ContainsKey(Helpers.Constants.ExtensionAttributeName))
            {
                var extensionAttribute = user.AdditionalData[Helpers.Constants.ExtensionAttributeName] as string;
                if (long.TryParse(extensionAttribute, out var gitHubId))
                {
                    dto.GitHubId = gitHubId;
                }
            }
            return dto;
        }
    }
}
