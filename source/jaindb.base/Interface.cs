using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JainDBProvider
{
    public interface IStore
    {
        string Name { get; }
        Dictionary<string, string> Settings { get; set; }
        List<string> GetAllIDs();

        IAsyncEnumerable<JObject> GetRawAssetsAsync(string paths);

        void Init();

        string LookupID(string name, string value);

        string ReadHash(string Hash, string Collection);

        int totalDeviceCount(string sPath = "");

        bool WriteHash(string Hash, string Data, string Collection);
        bool WriteLookupID(string name, string value, string id);
    }


}
