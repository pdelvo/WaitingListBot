using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WaitingListBot.Data
{
    public class InvitedUser
    {
        public int Id { get; set; }
        public Invite Invite { get; set; }

        public DateTime InviteTime { get; set; }
        public bool? InviteAccepted { get; set; }
        public UserInGuild User { get; set; }

        public ulong DmQuestionMessageId { get; set; }
    }
}
