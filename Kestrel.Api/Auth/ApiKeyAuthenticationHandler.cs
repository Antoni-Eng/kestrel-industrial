using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Kestrel.Core.Configuration;

namespace Kestrel.Api.Auth;

public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<AuthOptions> authOptions) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var configured = authOptions.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(configured))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!Request.Headers.TryGetValue(HeaderName, out var provided) || string.IsNullOrWhiteSpace(provided))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var expected = Encoding.UTF8.GetBytes(configured);
        var actual = Encoding.UTF8.GetBytes(provided.ToString());
        if (expected.Length != actual.Length || !CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "api-key"),
                new Claim(ClaimTypes.Name, "eval"),
                new Claim(ClaimTypes.AuthenticationMethod, SchemeName)
            ],
            SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
