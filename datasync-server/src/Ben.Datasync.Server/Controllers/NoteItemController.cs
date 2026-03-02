// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Datasync.Server;
using CommunityToolkit.Datasync.Server.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ben.Datasync.Server
{

    [Authorize]
    [Route("tables/[controller]")]
    [ApiExplorerSettings(IgnoreApi = false)]
    public class NoteItemController : TableController<NoteItem>
    {

        // public NoteItemController(AppDbContext context) 
        //     : base(new EntityTableRepository<NoteItem>(context))
        // {
        // }

        public NoteItemController(AppDbContext context, IHttpContextAccessor contextAccessor, ILogger<PersonalAccessControlProvider<NoteItem>> logger) : base()
        {
            Repository = new EntityTableRepository<NoteItem>(context);
            AccessControlProvider = new PersonalAccessControlProvider<NoteItem>(contextAccessor, logger);
        }

    }

}