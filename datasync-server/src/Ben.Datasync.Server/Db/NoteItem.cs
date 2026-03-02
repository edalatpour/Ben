// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Datasync.Server.EntityFrameworkCore;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace Ben.Datasync.Server
{

    public class NoteItem : EntityTableData, IPersonalEntity
    {
        [JsonIgnore]
        public string UserId { get; set; } = string.Empty;

        public DateTime Key { get; set; }

        public int Order { get; set; }

        [Required, MinLength(1)]
        public string Text { get; set; } = string.Empty;

    }

}

