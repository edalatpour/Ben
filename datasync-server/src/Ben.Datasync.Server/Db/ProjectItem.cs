using CommunityToolkit.Datasync.Server.EntityFrameworkCore;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace Ben.Datasync.Server
{
    public class ProjectItem : EntityTableData, IPersonalEntity
    {
        [JsonIgnore]
        [MaxLength(256)]
        public string UserId { get; set; } = string.Empty;

        [Required, MaxLength(128)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(128)]
        public string NormalizedName { get; set; } = string.Empty;
    }
}