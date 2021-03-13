using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WaitingListBot
{
    public class StorageFactory
    {
        readonly Dictionary<ulong, Storage> storages = new Dictionary<ulong, Storage>();

        public Storage GetStorage(ulong guildId)
        {
            var storage = storages.GetValueOrDefault(guildId);

            if (storage != null)
            {
                return storage;
            }

            storage = Storage.LoadForGuild(guildId);

            storages.Add(guildId, storage);

            return storage;
        }

        public IEnumerable<ulong> ListIds()
        {
            foreach (var item in Directory.EnumerateFiles(Storage.GuildDirectory))
            {
                var fileName = Path.GetFileNameWithoutExtension(item);

                if (ulong.TryParse(fileName, out ulong id))
                {
                    yield return id;
                }
            }
        }
    }
}
