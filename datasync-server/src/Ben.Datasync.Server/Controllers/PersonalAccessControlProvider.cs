using System;
using System.Linq.Expressions;
using CommunityToolkit.Datasync.Server;
using CommunityToolkit.Datasync.Server.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ben.Datasync.Server
{
  public class PersonalAccessControlProvider<TEntity>(IHttpContextAccessor contextAccessor, ILogger<PersonalAccessControlProvider<TEntity>> logger) : IAccessControlProvider<TEntity>
    where TEntity : ITableData, IPersonalEntity
  {

    // private string? UserId { get => contextAccessor.HttpContext?.User?.Identity?.Name; }
    private string? UserId
    {
      get => contextAccessor.HttpContext?.User?.FindFirst("email")?.Value
          ?? contextAccessor.HttpContext?.User?.FindFirst("preferred_username")?.Value
          ?? contextAccessor.HttpContext?.User?.Identity?.Name;
    }

    public Expression<Func<TEntity, bool>> GetDataView()
    {
      logger.LogInformation("GetDataView called. UserId: {UserId}", UserId ?? "(null)");
      return UserId is null ? x => false : x => x.UserId == UserId;
    }

    public ValueTask<bool> IsAuthorizedAsync(TableOperation op, TEntity? entity, CancellationToken cancellationToken = default)
    {
      logger.LogInformation("IsAuthorizedAsync called. Operation: {Operation}, UserId: {UserId}, Entity.UserId: {EntityUserId}",
        op, UserId ?? "(null)", entity?.UserId ?? "(null)");
      return ValueTask.FromResult(op is TableOperation.Create || op is TableOperation.Query || entity.UserId == UserId);
    }

    public ValueTask PreCommitHookAsync(TableOperation op, TEntity entity, CancellationToken cancellationToken = default)
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

      logger.LogInformation("UserId extracted from Identity.Name: {UserId}", UserId ?? "(null)");

      if (UserId is null)
      {
        logger.LogWarning("UserId is null in PreCommitHookAsync! Cannot assign to entity.");
      }

      entity.UserId = UserId;
      return ValueTask.CompletedTask;
    }

    public ValueTask PostCommitHookAsync(TableOperation op, TEntity entity, CancellationToken cancellationToken = default)
      => ValueTask.CompletedTask;
  }

}