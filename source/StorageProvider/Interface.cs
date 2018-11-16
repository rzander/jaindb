using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JainDBProvider
{
    public interface IStore
    {
        Dictionary<string,string> Settings { get; set; }

        string Name { get; }

        void Init();

        bool WriteHash(string Hash, string Data, string Collection);

        string ReadHash(string Hash, string Collection);

        int totalDeviceCount(string sPath = "");

        IEnumerable<JObject> GetRawAssets(string paths);

        string LookupID(string name, string value);

        bool WriteLookupID(string name, string value, string id);

        List<string> GetAllIDs();


    }


}
