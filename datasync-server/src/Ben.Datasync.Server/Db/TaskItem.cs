// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Datasync.Server.EntityFrameworkCore;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace Ben.Datasync.Server
{

    public class TaskItem : EntityTableData, IPersonalEntity
    {

        [JsonIgnore]
        public string UserId { get; set; } = string.Empty;

        [Required, MaxLength(128)]
        public string Key { get; set; } = string.Empty;

        public string Status { get; set; }

        public string Priority { get; set; }

        public int Order { get; set; }

        [Required, MinLength(1)]
        public string Title { get; set; } = string.Empty;

        // Lineage
        public string? ParentTaskId { get; set; }
        public TaskItem? ParentTask { get; set; }

        public string? OriginalTaskId { get; set; }
        public TaskItem? OriginalTask { get; set; }

        public List<TaskItem> ForwardedChildren { get; set; } = new();

    }

}

