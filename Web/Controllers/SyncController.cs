using Web.Helpers;
using Web.Model;
using Web.Services;
using Microsoft.AspNetCore.Mvc;
using Octokit;
using System.Text;

namespace Web.Controllers
{
    [ApiController]
    [Route("api/sync")]
    public class SyncController : Controller
    {
        private readonly string _privateKeyPem;
        private readonly string _clientId;
        private readonly string _appId;
        private readonly MicrosoftGraphService _microsoftGraph;
        private readonly ILogger _logger;
        public SyncController(IConfiguration configuration, MicrosoftGraphService microsoftGraph, ILoggerFactory loggerFactory)
        {
            _privateKeyPem = Encoding.UTF8.GetString(Convert.FromBase64String(configuration["GitHubProvisioning:PrivateKey"]));
            _clientId = configuration["GitHubProvisioning:ClientId"];
            _appId = configuration["GitHubProvisioning:AppId"];
            _microsoftGraph = microsoftGraph;
            _logger = loggerFactory.CreateLogger<SyncController>();
        }
        public async Task<IActionResult> Index()
        {
            var appClient = new GitHubClient(new ProductHeaderValue(Constants.UserAgent), new GitHubAppCredentialStore(long.Parse(_appId), _privateKeyPem));;
            var installations = await appClient.GitHubApps.GetAllInstallationsForCurrent();

            //installations = installations.Where(i => i.Account.Login == "NETWORG").ToList();
            foreach (var installation in installations)
            {
                var orgLogin = installation.Account.Login;
                var installationToken = await appClient.GitHubApps.CreateInstallationToken(installation.Id);

                var installationClient = new GitHubClient(new ProductHeaderValue(Constants.UserAgent))
                {
                    Credentials = new Credentials(installationToken.Token)
                };
                var teamsEntraMapping = new List<GitHubTeam>();
                var orgs = await installationClient.Organization.Get(orgLogin);
                var orgMembers = await installationClient.Organization.Member.GetAll(orgLogin);
                var pendingInvitations = await installationClient.Organization.Member.GetAllPendingInvitations(orgLogin);
                var pendingInvitationsWithUserId = new Dictionary<OrganizationMembershipInvitation, long>();
                var failedInvitations = await installationClient.Organization.Member.GetAllFailedInvitations(orgLogin);
                foreach(var invitation in failedInvitations)
                {
                    //await installationClient.Organization.Member.CancelOrganizationInvitation(orgLogin, invitation.Id);
                }
                foreach (var invitation in pendingInvitations)
                {
                    var ghUser = installationClient.User.Get(invitation.Login);
                    if (ghUser != null)
                    {
                        pendingInvitationsWithUserId.Add(invitation, ghUser.Id);
                    }
                }

                var teams = await installationClient.Organization.Team.GetAll(orgLogin);
                foreach(var team in teams)
                {
                    var description = team.Description;
                    if (!string.IsNullOrWhiteSpace(description) && description.Contains("Entra:", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = description.Split("Entra:", 2);
                        if (parts.Length == 2)
                        {
                            var groupId = parts[1].Trim();
                            if (!string.IsNullOrWhiteSpace(groupId))
                            {
                                teamsEntraMapping.Add(new GitHubTeam
                                {
                                    Id = team.Id,
                                    EntraId = groupId
                                });
                            }
                        }
                    }
                }

                var entraUsers = new List<GraphUserDto>();
                foreach (var team in teamsEntraMapping)
                {
                    var users = await _microsoftGraph.GetGroupMembers(team.EntraId);

                    // Filter out users currently with a pending invitation
                    users = users.Where(u => !pendingInvitationsWithUserId.Values.Contains(u.GitHubId ?? 0)).ToList();
                    team.Members = users;
                    entraUsers.AddRange(users);
                }
                entraUsers = entraUsers.GroupBy(u => u.Id).Select(g => g.First()).ToList();

                // Users to invite from Entra
                foreach (var eu in entraUsers)
                {
                    var linkedGhUser = orgMembers.FirstOrDefault(u => u.Id == eu.GitHubId);
                    if (linkedGhUser == null)
                    {
                        //await installationClient.Organization.Member.CreateOrganizationInvitation(orgLogin, new OrganizationInvitationRequest(eu.GitHubId.GetValueOrDefault()));
                    }
                }

                // Users to remove from GitHub
                foreach (var gh in orgMembers)
                {
                    var linkedEu = entraUsers.FirstOrDefault(u => u.GitHubId == gh.Id);
                    if (linkedEu == null)
                    {
                        if(Constants.ExemptUsers.Contains(gh.Login))
                        {
                            continue;
                        }
                        //await installationClient.Organization.Member.RemoveOrganizationMembership(orgLogin, gh.Login);
                    }
                }
                // Users from pending invitations not in current users to cancel
                foreach(var gh in pendingInvitationsWithUserId)
                {
                    var found = entraUsers.FirstOrDefault(x => x.GitHubId == gh.Value);
                    if(found == null)
                    {
                        //await installationClient.Organization.Member.CancelOrganizationInvitation(orgLogin, gh.Key.Id);
                    }
                }

                foreach(var team in teamsEntraMapping)
                {
                    var teamMembers = await installationClient.Organization.Team.GetAllMembers(team.Id);
                    // Remove members not in Entra group
                    foreach (var member in teamMembers)
                    {
                        var linkedEu = team.Members.FirstOrDefault(u => u.GitHubId == member.Id);
                        if (linkedEu == null)
                        {
                            await installationClient.Organization.Team.RemoveMembership(team.Id, member.Login);
                        }
                    }

                    // Add members from Entra group
                    foreach (var eu in team.Members)
                    {
                        var linkedGhUser = teamMembers.FirstOrDefault(u => u.Id == eu.GitHubId);
                        if (linkedGhUser == null)
                        {
                            var ghUser = orgMembers.FirstOrDefault(u => u.Id == eu.GitHubId);
                            if (ghUser != null)
                            {
                                await installationClient.Organization.Team.AddOrEditMembership(team.Id, ghUser.Login, new UpdateTeamMembership(TeamRole.Member));
                            }
                        }
                    }
                }
            }

            return new OkObjectResult(new { });
        }
    }
}