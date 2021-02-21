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
        const string name = "savestate.json";

        public Storage()
        {
            List = new List<UserInList>();
            PlayCounter = new List<PlayCounter>();
        }

        public void Save()
        {
            var json = JsonConvert.SerializeObject(this);
            File.WriteAllText(name, json);
        }

        public static Storage FromFile()
        {
            try
            {
                return JsonConvert.DeserializeObject<Storage>(File.ReadAllText(name));
            }
            catch (Exception)
            {
                return new Storage();
            }
        }

        public ulong WaitingListChannelId { get; set; }

        public List<UserInList> List { get; set; }

        public List<PlayCounter> PlayCounter { get; set; }
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
