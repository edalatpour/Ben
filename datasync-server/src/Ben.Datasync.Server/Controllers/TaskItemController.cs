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
    public class TaskItemController : TableController<TaskItem>
    {
        // public TaskItemController(AppDbContext context) 
        //     : base(new EntityTableRepository<TaskItem>(context))
        // {
        // }

        public TaskItemController(AppDbContext context, IHttpContextAccessor contextAccessor, ILogger<PersonalAccessControlProvider<TaskItem>> logger) : base()
        {
            Repository = new EntityTableRepository<TaskItem>(context);
            AccessControlProvider = new PersonalAccessControlProvider<TaskItem>(contextAccessor, logger);
        }

    }

}

