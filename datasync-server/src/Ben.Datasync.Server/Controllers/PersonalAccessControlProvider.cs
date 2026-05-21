using System;
using System.Linq.Expressions;
using System.Security.Claims;
using CommunityToolkit.Datasync.Server;
using CommunityToolkit.Datasync.Server.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ben.Datasync.Server
{
  public class PersonalAccessControlProvider<TEntity>(IHttpContextAccessor contextAccessor, ILogger<PersonalAccessControlProvider<TEntity>> logger) : IAccessControlProvider<TEntity>
    where TEntity : ITableData, IPersonalEntity
  {
    private const string UserRecordCheckedItemKey = "Ben.UserRecordChecked";
    private const string CanonicalUserIdItemKey = "Ben.CanonicalUserId";

    private string? EmailUserId
    {
      get => contextAccessor.HttpContext?.User?.FindFirst("email")?.Value
          ?? contextAccessor.HttpContext?.User?.FindFirst("preferred_username")?.Value
          ?? contextAccessor.HttpContext?.User?.Identity?.Name;
    }

    public Expression<Func<TEntity, bool>> GetDataView()
    {
      string? canonicalUserId = GetCanonicalUserIdForDataView();
      logger.LogInformation("GetDataView called. CanonicalUserId: {CanonicalUserId}", canonicalUserId ?? "(null)");
      return canonicalUserId is null ? x => false : x => x.UserId == canonicalUserId;
    }

    public async ValueTask<bool> IsAuthorizedAsync(TableOperation op, TEntity? entity, CancellationToken cancellationToken = default)
    {
      string? userId = await EnsureUserRecordExistsAndGetCanonicalUserIdAsync(cancellationToken);
      logger.LogInformation("IsAuthorizedAsync called. Operation: {Operation}, CanonicalUserId: {UserId}, Entity.UserId: {EntityUserId}",
        op, userId ?? "(null)", entity?.UserId ?? "(null)");

      if (string.IsNullOrWhiteSpace(userId))
      {
        return false;
      }

      return op is TableOperation.Create || op is TableOperation.Query || (entity?.UserId == userId);
    }

    public async ValueTask PreCommitHookAsync(TableOperation op, TEntity entity, CancellationToken cancellationToken = default)
    {
      var httpContext = contextAccessor.HttpContext;

      // Log HttpContext existence and basic request info
      logger.LogInformation("PreCommitHookAsync called. Operation: {Operation}", op);
      logger.LogInformation("HttpContext available: {HasContext}", httpContext != null);

      if (httpContext != null)
      {
        logger.LogInformation("Request Path: {Path}, Method: {Method}", httpContext.Request.Path, httpContext.Request.Method);

        // Log Authorization header (auth token)
        var authHeader = httpContext.Request.Headers.Authorization.FirstOrDefault();
        logger.LogInformation("Authorization Header: {AuthHeader}", authHeader ?? "(not provided)");

        logger.LogInformation("User exists: {HasUser}, IsAuthenticated: {IsAuthenticated}",
          httpContext.User != null, httpContext.User?.Identity?.IsAuthenticated ?? false);

        if (httpContext.User?.Identity != null)
        {
          logger.LogInformation("Identity Name: {Name}, AuthType: {AuthType}",
            httpContext.User.Identity.Name ?? "(null)",
            httpContext.User.Identity.AuthenticationType ?? "(null)");

          // Log all claims
          if (httpContext.User.Claims.Any())
          {
            foreach (var claim in httpContext.User.Claims)
            {
              logger.LogInformation("Claim - Type: {ClaimType}, Value: {ClaimValue}", claim.Type, claim.Value);
            }
          }
          else
          {
            logger.LogInformation("No claims found in user identity");
          }
        }
        else
        {
          logger.LogWarning("User.Identity is null");
        }
      }
      else
      {
        logger.LogWarning("HttpContext is null - authentication context unavailable");
      }

      string? userId = await EnsureUserRecordExistsAndGetCanonicalUserIdAsync(cancellationToken);
      logger.LogInformation("Canonical user id resolved: {UserId}", userId ?? "(null)");

      if (string.IsNullOrWhiteSpace(userId))
      {
        logger.LogWarning("UserId is null in PreCommitHookAsync. Skipping user assignment and relying on IsAuthorizedAsync to reject the operation.");
        return;
      }

      entity.UserId = userId;
    }

    public ValueTask PostCommitHookAsync(TableOperation op, TEntity entity, CancellationToken cancellationToken = default)
      => ValueTask.CompletedTask;

    private string? GetCanonicalUserIdForDataView()
    {
      HttpContext? httpContext = contextAccessor.HttpContext;
      if (httpContext == null)
      {
        return null;
      }

      if (httpContext.Items.TryGetValue(CanonicalUserIdItemKey, out object? cachedCanonical)
          && cachedCanonical is string cachedCanonicalUserId
          && !string.IsNullOrWhiteSpace(cachedCanonicalUserId))
      {
        return cachedCanonicalUserId;
      }

      string? externalId = httpContext.User?.FindFirst("sub")?.Value;
      string? identityProvider = ResolveIdentityProvider(httpContext.User);
      if (string.IsNullOrWhiteSpace(externalId) || string.IsNullOrWhiteSpace(identityProvider))
      {
        return EmailUserId;
      }

      string normalizedExternalId = externalId.Length > 200 ? externalId[..200] : externalId;
      string normalizedIdentityProvider = identityProvider.Length > 50 ? identityProvider[..50] : identityProvider;

      AppDbContext dbContext = httpContext.RequestServices.GetRequiredService<AppDbContext>();
      UserRecord? existing = dbContext.Users
        .AsNoTracking()
        .FirstOrDefault(user => user.ExternalId == normalizedExternalId && user.IdentityProvider == normalizedIdentityProvider);

      if (existing == null)
      {
        return EmailUserId;
      }

      string canonicalUserId = existing.UserId.ToString();
      httpContext.Items[CanonicalUserIdItemKey] = canonicalUserId;
      httpContext.Items[UserRecordCheckedItemKey] = true;
      return canonicalUserId;
    }

    private async ValueTask<string?> EnsureUserRecordExistsAndGetCanonicalUserIdAsync(CancellationToken cancellationToken)
    {
      HttpContext? httpContext = contextAccessor.HttpContext;
      if (httpContext == null)
      {
        return null;
      }

      if (httpContext.Items.TryGetValue(CanonicalUserIdItemKey, out object? cachedCanonical)
          && cachedCanonical is string cachedCanonicalUserId
          && !string.IsNullOrWhiteSpace(cachedCanonicalUserId))
      {
        return cachedCanonicalUserId;
      }

      if (httpContext.Items.ContainsKey(UserRecordCheckedItemKey))
      {
        return EmailUserId;
      }

      httpContext.Items[UserRecordCheckedItemKey] = true;

      string? externalId = httpContext.User?.FindFirst("sub")?.Value;
      string? identityProvider = ResolveIdentityProvider(httpContext.User);
      string? email = EmailUserId;

      if (string.IsNullOrWhiteSpace(externalId) || string.IsNullOrWhiteSpace(identityProvider))
      {
        logger.LogWarning("Skipping Users upsert: missing ExternalId or IdentityProvider. ExternalId={ExternalId}, IdentityProvider={IdentityProvider}",
          externalId ?? "(null)",
          identityProvider ?? "(null)");
        return email;
      }

      string normalizedExternalId = externalId.Length > 200 ? externalId[..200] : externalId;
      string normalizedIdentityProvider = identityProvider.Length > 50 ? identityProvider[..50] : identityProvider;
      string? normalizedEmail = string.IsNullOrWhiteSpace(email)
        ? null
        : (email.Length > 200 ? email[..200] : email);

      AppDbContext dbContext = httpContext.RequestServices.GetRequiredService<AppDbContext>();

      UserRecord? existing = await dbContext.Users
        .FirstOrDefaultAsync(
          user => user.ExternalId == normalizedExternalId && user.IdentityProvider == normalizedIdentityProvider,
          cancellationToken);

      if (existing != null)
      {
        if (string.IsNullOrWhiteSpace(existing.Email) && !string.IsNullOrWhiteSpace(normalizedEmail))
        {
          existing.Email = normalizedEmail;
          await dbContext.SaveChangesAsync(cancellationToken);
        }

        string existingCanonicalUserId = existing.UserId.ToString();
        httpContext.Items[CanonicalUserIdItemKey] = existingCanonicalUserId;
        logger.LogInformation("Users record already exists for provider {IdentityProvider} and external id {ExternalId}",
          normalizedIdentityProvider,
          normalizedExternalId);
        return existingCanonicalUserId;
      }

      var newUser = new UserRecord
      {
        UserId = Guid.NewGuid(),
        ExternalId = normalizedExternalId,
        IdentityProvider = normalizedIdentityProvider,
        Email = normalizedEmail,
        CreatedAt = DateTime.UtcNow
      };

      dbContext.Users.Add(newUser);

      try
      {
        await dbContext.SaveChangesAsync(cancellationToken);
      }
      catch (DbUpdateException)
      {
        UserRecord? racedExisting = await dbContext.Users
          .AsNoTracking()
          .FirstOrDefaultAsync(
            user => user.ExternalId == normalizedExternalId && user.IdentityProvider == normalizedIdentityProvider,
            cancellationToken);

        if (racedExisting == null)
        {
          throw;
        }

        string racedCanonicalUserId = racedExisting.UserId.ToString();
        httpContext.Items[CanonicalUserIdItemKey] = racedCanonicalUserId;
        return racedCanonicalUserId;
      }

      string canonicalUserId = newUser.UserId.ToString();
      httpContext.Items[CanonicalUserIdItemKey] = canonicalUserId;

      if (!string.IsNullOrWhiteSpace(normalizedEmail))
      {
        int tasksUpdated = await dbContext.TaskItems
          .Where(task => task.UserId == normalizedEmail)
          .ExecuteUpdateAsync(setters => setters.SetProperty(task => task.UserId, canonicalUserId), cancellationToken);

        int notesUpdated = await dbContext.NoteItems
          .Where(note => note.UserId == normalizedEmail)
          .ExecuteUpdateAsync(setters => setters.SetProperty(note => note.UserId, canonicalUserId), cancellationToken);

        int projectsUpdated = await dbContext.ProjectItems
          .Where(project => project.UserId == normalizedEmail)
          .ExecuteUpdateAsync(setters => setters.SetProperty(project => project.UserId, canonicalUserId), cancellationToken);

        logger.LogInformation("Backfilled legacy UserId from email to GUID for {Email}. Tasks={TasksUpdated}, Notes={NotesUpdated}, Projects={ProjectsUpdated}",
          normalizedEmail,
          tasksUpdated,
          notesUpdated,
          projectsUpdated);
      }

      logger.LogInformation("Inserted Users record. UserId={UserId}, IdentityProvider={IdentityProvider}, ExternalId={ExternalId}, Email={Email}",
        newUser.UserId,
        newUser.IdentityProvider,
        newUser.ExternalId,
        newUser.Email ?? "(null)");

      return canonicalUserId;
    }

    private static string? ResolveIdentityProvider(ClaimsPrincipal? user)
    {
      if (user == null)
      {
        return null;
      }

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