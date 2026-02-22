// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Datasync.Server;
using CommunityToolkit.Datasync.Server.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sample.Datasync.Server.Db;

namespace Sample.Datasync.Server.Controllers;

[Route("tables/[controller]")]
[ApiExplorerSettings(IgnoreApi = false)]
[Authorize]
public class NoteItemController : TableController<NoteItem>
{
    public NoteItemController(AppDbContext context, IAccessControlProvider<NoteItem> accessControlProvider) 
        : base(new EntityTableRepository<NoteItem>(context), accessControlProvider)
    {
    }
}