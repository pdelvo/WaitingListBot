using System;

namespace WaitingListBot.Data
{
    public class UserInGuild
    {
        public int Id { get; set; }

        public GuildData Guild { get; set; }

        public ulong UserId { get; set; }

        public int PlayCount { get; set; }

        public string? Name { get; set; }

        public bool IsSub { get; set; }

        public bool IsInWaitingList { get; set; }

        public DateTime? JoinTime { get; set; }
    }
}