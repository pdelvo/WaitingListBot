using Discord;

using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WaitingListBot
{
    public class Storage
    {
        public const string GuildDirectory = "guilds";
        private ulong guildId;

        static Storage()
        {
            Directory.CreateDirectory(GuildDirectory);
        }

        public Storage()
        {
            List = new List<UserInList>();
            PlayCounter = new List<PlayCounter>();
            CommandPrefix = "wl.";
            DMMessageFormat = "You have been invited to play!\n Name: {0}\nPassword: {1}";
            Information = new GuildInformation();
        }

        public void Save()
        {
            var json = JsonConvert.SerializeObject(this);
            File.WriteAllText(Path.Combine(GuildDirectory, guildId + ".json"), json);
        }

        public static Storage LoadForGuild(ulong guildId)
        {
            try
            {
                var storage = JsonConvert.DeserializeObject<Storage>(File.ReadAllText(Path.Combine(GuildDirectory, guildId + ".json")));

                if (storage == null)
                {
                    return new Storage { guildId = guildId };
                }

                storage.guildId = guildId;
                return storage;
            }
            catch (Exception)
            {
                return new Storage { guildId = guildId };
            }
        }

        public GuildInformation Information { get; set; }

        public ulong WaitingListChannelId { get; set; }

        public List<UserInList> List { get; set; }

        public List<PlayCounter> PlayCounter { get; set; }

        public ulong SubRoleId { get; set; }

        public string DMMessageFormat { get; set; }

        public string CommandPrefix { get; set; }

        public bool IsEnabled { get; set; }

        [JsonIgnore]
        public bool IsInitialized { get; set; }

        public bool IsReactionMode { get; set; }

        public ulong BotMessageId { get; set; }

        public ulong ReactionMessageId { get; set; }

        public IEmote ReactionEmote { get; } = new Emoji("✅");


        public List<UserInListWithCounter> GetSortedList()
        {
            var newList = new List<UserInList>(List);
            newList.Sort((a, b) =>
            {
                if (GetPlayCounterById(a.Id) < GetPlayCounterById(b.Id))
                {
                    return -1;
                }
                else if (GetPlayCounterById(a.Id) > GetPlayCounterById(b.Id))
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
                return a.JoinTime.CompareTo(b.JoinTime);
            });

            return newList.Select((x) => new UserInListWithCounter(x, GetPlayCounterById(x.Id))).ToList();
        }

        private int GetPlayCounterById(ulong id)
        {
            return PlayCounter.SingleOrDefault(x => x.Id == id)?.Counter ?? 0;
        }
    }

    public record UserInList
    {
        public ulong Id { get; set; }

        public string? Name { get; set; }

        public bool IsSub { get; set; }

        public DateTime JoinTime { get; set; }
    }

    public record UserInListWithCounter : UserInList
    {
        public UserInListWithCounter()
        {

        }

        public UserInListWithCounter(UserInList userInList, int counter) : base(userInList)
        {
            this.Counter = counter;
        }
        public int Counter { get; set; }
    }

    public class PlayCounter
    {
        public ulong Id { get; set; }

        public int Counter { get; set; }
    }
}
