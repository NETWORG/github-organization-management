using Microsoft.IdentityModel.Tokens;
using Octokit;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;

namespace Web.Helpers
{
    public class GitHubAppCredentialStore : ICredentialStore
    {
        private readonly long _appId;
        private readonly string _privateKeyPem;

        private readonly RSA _rsa = RSA.Create();

        private Credentials? _cached;
        private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

        public GitHubAppCredentialStore(long appId, string privateKeyPem)
        {
            _appId = appId;
            _privateKeyPem = privateKeyPem;
        }

        public async Task<Credentials> GetCredentials()
        {
            if (_cached == null || DateTimeOffset.UtcNow >= _expiresAt)
            {
                var appJwt = CreateAppJwt(_appId, _privateKeyPem);

                _cached = new Credentials(appJwt, AuthenticationType.Bearer);
                _expiresAt = DateTimeOffset.UtcNow.AddMinutes(-2);
            }

            return _cached;
        }
        private string CreateAppJwt(long appId, string privateKeyPem)
        {
            _rsa.ImportFromPem(privateKeyPem.AsSpan());

            var credentials = new SigningCredentials(new RsaSecurityKey(_rsa), SecurityAlgorithms.RsaSha256);
            var now = DateTimeOffset.UtcNow;

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Issuer = _appId.ToString(),
                IssuedAt = now.UtcDateTime,
                Expires = now.AddMinutes(9).UtcDateTime,
                SigningCredentials = credentials
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);

            return tokenHandler.WriteToken(token);
        }
    }
}
