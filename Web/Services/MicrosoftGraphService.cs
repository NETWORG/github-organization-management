using Web.Model;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace Web.Services
{
    public class MicrosoftGraphService
    {
        public GraphServiceClient _microsoftGraph { get; }
        public MicrosoftGraphService(GraphServiceClient graphServiceClient)
        {
            _microsoftGraph = graphServiceClient;
        }

        public async Task<IEnumerable<GraphUserDto>> GetGroupMembers(string groupId)
        {
            var entraUsers = new List<User>();
            var firstPage = await _microsoftGraph.Groups[groupId].TransitiveMembers.GraphUser.GetAsync(x => {
                x.QueryParameters.Select = ["id", "userPrincipalName", "accountEnabled", Helpers.Constants.ExtensionAttributeName];
                x.Headers.Add("ConsistencyLevel", "eventual");
                //x.QueryParameters.Top = 10;
            });

            var iterator = PageIterator<User, UserCollectionResponse>
                .CreatePageIterator(_microsoftGraph, firstPage,
                    (member) =>
                    {
                        if (member.AccountEnabled == true && member.AdditionalData.ContainsKey(Helpers.Constants.ExtensionAttributeName))
                        {
                            entraUsers.Add(member);
                        }
                        return true;
                    });

            await iterator.IterateAsync();

            return entraUsers.Select(x => GraphUserDto.From(x));
        }
    }
}
