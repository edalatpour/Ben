// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Datasync.Server;
using Sample.Datasync.Server.Db;
using System.Linq.Expressions;
using System.Security.Claims;

namespace Sample.Datasync.Server;

/// <summary>
/// Access control provider that restricts data access to the authenticated user.
/// This implements the "personal table" pattern where users can only access their own data.
/// </summary>
/// <typeparam name="T">The entity type that implements IUserOwned</typeparam>
public class PersonalAccessControlProvider<T> : IAccessControlProvider<T>
    where T : class, ITableData, IUserOwned
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PersonalAccessControlProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Returns a filter expression that limits data to only records owned by the authenticated user.
    /// </summary>
    public Expression<Func<T, bool>>? GetDataView()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        
        if (user?.Identity?.IsAuthenticated != true)
        {
            // If user is not authenticated, return filter that matches nothing
            return _ => false;
        }

        // Get the user ID from claims (supports multiple claim types)
        string? userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value
            ?? user.FindFirst("oid")?.Value
            ?? user.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            // If user ID cannot be determined, return filter that matches nothing
            return _ => false;
        }

        // Filter to only entities owned by this user
        return entity => entity.UserId == userId;
    }

    /// <summary>
    /// Determines if the user is authorized to perform the given operation on the entity.
    /// </summary>
    public ValueTask<bool> IsAuthorizedAsync(TableOperation operation, T? entity, CancellationToken cancellationToken = default)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        
        if (user?.Identity?.IsAuthenticated != true)
        {
            return new ValueTask<bool>(false);
        }

        // Get the user ID from claims
        string? userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value
            ?? user.FindFirst("oid")?.Value
            ?? user.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return new ValueTask<bool>(false);
        }

        // For create operations, we'll set the UserId in PreCommitHookAsync
        // For other operations, the data view filter ensures the user owns the entity
        return new ValueTask<bool>(true);
    }

    /// <summary>
    /// Pre-commit hook - ensures UserId is set correctly before saving.
    /// </summary>
    public ValueTask PreCommitHookAsync(TableOperation operation, T entity, CancellationToken cancellationToken = default)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        
        if (user?.Identity?.IsAuthenticated == true && operation == TableOperation.Create)
        {
            string? userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? user.FindFirst("sub")?.Value
                ?? user.FindFirst("oid")?.Value
                ?? user.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;

            if (!string.IsNullOrEmpty(userId))
            {
                entity.UserId = userId;
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Post-commit hook - called after the entity has been saved.
    /// Can be used for logging, notifications, etc.
    /// </summary>
    public ValueTask PostCommitHookAsync(TableOperation operation, T entity, CancellationToken cancellationToken = default)
    {
        // No post-commit actions needed for this implementation
        return ValueTask.CompletedTask;
    }
}
