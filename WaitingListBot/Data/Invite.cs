﻿using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace WaitingListBot.Data
{
    public class Invite
    {
        public int Id { get; set; }

        public DateTime InviteTime { get; set; }

        public int NumberOfInvitedUsers { get; set; }

        public GuildData Guild { get; set; }

        public ICollection<InvitedUser> InvitedUsers { get; set; }

        [NotMapped]
        public string[]? FormatData
        {
            get
            {
                return JsonConvert.DeserializeObject<string[]>(string.IsNullOrEmpty(FormatDataJson) ? "{}" : FormatDataJson);
            }
            set
            {
                FormatDataJson = JsonConvert.SerializeObject(value);
            }
        }

        public string FormatDataJson { get; set; }

        public ulong InviteMessageId { get; set; }
        public ulong InviteMessageChannelId { get; set; }
    }
}
