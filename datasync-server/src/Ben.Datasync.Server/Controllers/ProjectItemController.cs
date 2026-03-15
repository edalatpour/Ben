using CommunityToolkit.Datasync.Server;
using CommunityToolkit.Datasync.Server.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ben.Datasync.Server
{
    [Authorize]
    [Route("tables/[controller]")]
    [ApiExplorerSettings(IgnoreApi = false)]
    public class ProjectItemController : TableController<ProjectItem>
    {
        public ProjectItemController(AppDbContext context, IHttpContextAccessor contextAccessor, ILogger<PersonalAccessControlProvider<ProjectItem>> logger) : base()
        {
            Repository = new EntityTableRepository<ProjectItem>(context);
            AccessControlProvider = new PersonalAccessControlProvider<ProjectItem>(contextAccessor, logger);
        }
    }
}