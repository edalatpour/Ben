// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Datasync.Server.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Sample.Datasync.Server.Db;

public class NoteItem : EntityTableData, IUserOwned
{
    public DateTime Key { get; set; }

    public int Order { get; set; }

    [Required, MinLength(1)]
    public string Text { get; set; } = string.Empty;

    [Required]
    public string UserId { get; set; } = string.Empty;
    
}