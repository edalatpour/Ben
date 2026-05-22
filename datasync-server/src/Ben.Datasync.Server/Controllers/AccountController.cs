using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ben.Datasync.Server
{
    [Authorize]
    [ApiController]
    [Route("account")]
    [ApiExplorerSettings(IgnoreApi = false)]
    public sealed class AccountController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            AppDbContext dbContext,
            IHttpContextAccessor httpContextAccessor,
            ILogger<AccountController> logger)
        {
            _dbContext = dbContext;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        [HttpPost("delete-cloud-data")]
        public async Task<IActionResult> DeleteCloudDataAsync(CancellationToken cancellationToken)
        {
            HttpContext? httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.User?.Identity?.IsAuthenticated != true)
            {
                return Unauthorized();
            }

            string? externalId = httpContext.User.FindFirst("sub")?.Value;
            string? identityProvider = ResolveIdentityProvider(httpContext.User);
            string? email = ResolveEmail(httpContext.User);

            if (string.IsNullOrWhiteSpace(externalId) || string.IsNullOrWhiteSpace(identityProvider))
            {
                return BadRequest(new
                {
                    status = "invalid_identity",
                    message = "Cannot resolve identity provider and external id for this account deletion request."
                });
            }

            string normalizedExternalId = externalId.Length > 200 ? externalId[..200] : externalId;
            string normalizedIdentityProvider = identityProvider.Length > 50 ? identityProvider[..50] : identityProvider;

            UserRecord? userRecord = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    user => user.ExternalId == normalizedExternalId && user.IdentityProvider == normalizedIdentityProvider,
                    cancellationToken);

            _logger.LogInformation(
                "Delete-cloud-data request accepted in scaffold mode. Provider={IdentityProvider}, HasUserRecord={HasUserRecord}, CanonicalUserId={CanonicalUserId}",
                normalizedIdentityProvider,
                userRecord != null,
                userRecord?.UserId.ToString() ?? "(null)");

            return StatusCode(StatusCodes.Status501NotImplemented, new
            {
                status = "not_implemented",
                message = "Delete cloud data endpoint scaffold is active, but deletion is not enabled yet.",
                hasUserRecord = userRecord != null,
                canonicalUserId = userRecord?.UserId.ToString(),
                identityProvider = normalizedIdentityProvider,
                email = userRecord?.Email ?? email,
            });
        }

        private static string? ResolveEmail(ClaimsPrincipal user)
        {
            return user.FindFirst("email")?.Value
                ?? user.FindFirst("preferred_username")?.Value
                ?? user.Identity?.Name;
        }

        private static string? ResolveIdentityProvider(ClaimsPrincipal user)
        {
            string? idpClaim = user.FindFirst("idp")?.Value;
            if (!string.IsNullOrWhiteSpace(idpClaim))
            {
                string fromIdp = idpClaim.ToLowerInvariant();
                if (fromIdp.Contains("apple"))
                {
                    return "apple";
                }

                if (fromIdp.Contains("google"))
                {
                    return "google";
                }

                if (fromIdp.Contains("microsoft") || fromIdp.Contains("live.com") || fromIdp.Contains("entra"))
                {
                    return "microsoft";
                }
            }

            string? issuer = user.FindFirst("iss")?.Value;
            if (string.IsNullOrWhiteSpace(issuer))
            {
                return null;
            }

            string normalizedIssuer = issuer.ToLowerInvariant();
            if (normalizedIssuer.Contains("appleid.apple.com"))
            {
                return "apple";
            }

            if (normalizedIssuer.Contains("accounts.google.com"))
            {
                return "google";
            }

            if (normalizedIssuer.Contains("microsoftonline.com") || normalizedIssuer.Contains("ciamlogin.com"))
            {
                return "microsoft";
            }

            return null;
        }
    }
}