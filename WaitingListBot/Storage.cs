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
        const string directory = "guilds";
        private ulong guildId;

        static Storage()
        {
            Directory.CreateDirectory(directory);
        }

        public Storage()
        {
            List = new List<UserInList>();
            PlayCounter = new List<PlayCounter>();
            CommandPrefix = "wl.";
            DMMessageFormat = "You have been invited to play!\n Name: {0}\nPassword: {1}";
        }

        public void Save()
        {
            var json = JsonConvert.SerializeObject(this);
            File.WriteAllText(Path.Combine(directory, guildId + ".json"), json);
        }

        public static Storage LoadForGuild(ulong guildId)
        {
            try
            {
                var storage = JsonConvert.DeserializeObject<Storage>(File.ReadAllText(Path.Combine(directory, guildId + ".json")));
                storage.guildId = guildId;
                return storage;
            }
            catch (Exception)
            {
                return new Storage { guildId = guildId };
            }
        }

        public ulong WaitingListChannelId { get; set; }

        public List<UserInList> List { get; set; }

        public List<PlayCounter> PlayCounter { get; set; }

        public ulong SubRoleId { get; set; }

        public string DMMessageFormat { get; set; }

        public string CommandPrefix { get; set; }

        public bool IsEnabled { get; set; }

        [JsonIgnore]
        public bool IsInitialized { get; set; }
    }
    public class UserInList
    {
        public ulong Id { get; set; }

        public string Name { get; set; }

        public bool IsSub { get; set; }

        public DateTime JoinTime { get; set; }
    }
    public class PlayCounter
    {
        public ulong Id { get; set; }

        public int Counter { get; set; }
    }
}
