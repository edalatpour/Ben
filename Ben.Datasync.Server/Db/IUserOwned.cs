// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Sample.Datasync.Server.Db;

/// <summary>
/// Interface for entities that are owned by a specific user.
/// This enables user-scoped data filtering for personal tables.
/// </summary>
public interface IUserOwned
{
    /// <summary>
    /// Gets or sets the user ID that owns this entity.
    /// </summary>
    string UserId { get; set; }
}
