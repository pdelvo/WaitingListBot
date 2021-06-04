using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace WaitingListBot.Data
{
    public class GuildData
    {
        [Key]
        public int Id { get; set; }

        public ulong GuildId { get; set; }

        public string? Name { get; set; }

        public string? IconUrl { get; set; }

        public string? Description { get; set; }

        public string CommandPrefix { get; set; } = "wl.";

        public string DMMessageFormat { get; set; } = "You have been invited to play!\n Name: {0}\nPassword: {1}";

        public ICollection<Invite> Invites { get; set; }

        [NotMapped]
        public IEnumerable<UserInGuild> UsersInList => UsersInGuild.Where(x => x.IsInWaitingList);

        public ICollection<UserInGuild> UsersInGuild { get; set; }

        public ulong WaitingListChannelId { get; set; }

        public ulong SubRoleId { get; set; }

        public bool IsEnabled { get; set; }

        public ulong PublicMessageId { get; set; }

        public bool IsPaused { get; set; }

        public UserInGuild GetOrCreateGuildUser(ulong userId, string name)
        {
            var user = GetUser(userId);

            if (user == null)
            {
                user = new UserInGuild
                {
                    UserId = userId,
                    Name = name,
                    Guild = this
                };

                UsersInGuild.Add(user);
            }
            else
            {
                user.Name = name;
            }

            return GetUser(userId)!;
        }

        public UserInGuild? GetUser(ulong userId)
        {
            return UsersInGuild.SingleOrDefault(x => x.UserId == userId);
        }

        public List<UserInGuild> GetSortedList()
        {
            var newList = new List<UserInGuild>(UsersInList);
            newList.Sort((a, b) =>
            {
                if (a.PlayCount < b.PlayCount)
                {
                    return -1;
                }
                else if (a.PlayCount > b.PlayCount)
                {
                    return 1;
                }

                if (a.IsSub && !b.IsSub)
                {
                    return -1;
                }
                else if (!a.IsSub && b.IsSub)
                {
                    return 1;
                }
                return a.JoinTime!.Value.CompareTo(b.JoinTime!);
            });

            return newList;
        }
    }
}
