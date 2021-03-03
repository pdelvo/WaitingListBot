using System;
using System.Collections.Generic;
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
    }
}
