# GitHub Organization Management

This tool synchronizes GitHub organizations and team memberships based on the master data sourced from Entra ID (Azure AD). It allows users to link their GitHub account to their Entra ID account and then keeps the users periodically in sync.

This was made to avoid dependency on GitHub Enterprise (features like SCIM and SSO) which is too expensive for small organizations (and compared to Azure DevOps pricing).

Yet easily allows you to handle both open source and private repositories in the same organization while keeping acess driven through Entra ID (and groups).

## Features
* Invite users to GitHub organizations
* Remove disabled/deleted/out-of-scope users from GitHub organizations
* Maintain team memberships based on Entra ID group memberships
* Supports B2B guest accounts and their membership in groups
* Doesn't require GitHub Enterprise
* Doesn't interfere with external collaborators
* Doesn't interfere with direct assignments and teams not linked to Entra ID groups
* Supports multiple organizations against a single Entra ID tenant
* Exempt users (e.g. service accounts, admins) from organization removal (in case things go wrong)

## Limitations
* Group memberships are [flattened](https://learn.microsoft.com/en-us/graph/api/group-list-transitivemembers?view=graph-rest-1.0&tabs=http) (eg. nested groups are not carried over, but all the members are)

## Setup
1. Register GitHub OAuth application
1. Register GitHub App and obtain certificate (encode it as base64)
1. Provisioning the GitHub App into your organizations
1. Register Entra ID application and create [directory schema extension](https://learn.microsoft.com/en-us/graph/api/resources/extensionproperty?view=graph-rest-1.0)
    * User scopes: `openid`, `profile`, `User.Read`
    * Application scopes: `User.ReadWrite.All` (necessary for writing the extension value - account linking), `Group.Read.All`

```
POST https://graph.microsoft.com/v1.0/applications(AppId='<appId>')/extensionProperties
```
```json
{
    "name": "githubId",
    "dataType": "String",
    "isMultiValued": false,
    "targetObjects": [
        "User"
    ]
}
```

## Configuration

The application doesn't store any state itself, all the data is persisted in Entra ID and GitHub.

### Secrets reference
The application secrets can be also passed in as [environment variables](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-9.0#non-prefixed-environment-variables) instead of a JSON file.

```json
{
  "AzureAd": {
    "ClientId": "<entra_client_id>",
    "ClientSecret": "<entra_client_secret>",
    "TenantId": "<domain_or_tenant_id>",
    "Instance": "https://login.microsoftonline.com/",
    "ExtensionAttributeName": "<directory_schema_extension_name>"
  },
  "GitHub": {
    "ClientId": "<github_oauth_client_id>",
    "ClientSecret": "<github_oauth_client_secret>"
  },
  "GitHubProvisioning": {
    "AppId": <github_app_id>,
    "ClientId": "<github_app_client_id>",
    "PrivateKey": "<base64_encoded_private_key>"
  },
  "ExemptUsers": [
    "<Case_Sensitive_List_Of_Users>"
  ]
}
```

### GitHub Teams Configuration

Simply [create a team](https://docs.github.com/en/organizations/organizing-members-into-teams/creating-a-team) in your GitHub organization and fill the description field with your desired description and append `Entra: <entra_group_id>` to the end of the description. This will tell the tool to synchronize membership of the team with the specified group.