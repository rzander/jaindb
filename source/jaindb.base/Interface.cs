using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace JainDBProvider
{
    public interface IStore
    {
        string Name { get; }
        Dictionary<string, string> Settings { get; set; }

        Task<List<string>> GetAllIDsAsync(CancellationToken ct);

        IAsyncEnumerable<JObject> GetRawAssetsAsync(string paths, CancellationToken ct);

        void Init();

        Task<string> LookupIDAsync(string name, string value, CancellationToken ct);

        Task<string> ReadHashAsync(string Hash, string Collection, CancellationToken ct);

        Task<int> totalDeviceCountAsync(string sPath = "", CancellationToken ct = default(CancellationToken));

        Task<bool> WriteHashAsync(string Hash, string Data, string Collection, CancellationToken ct);

        Task<bool> WriteLookupIDAsync(string name, string value, string id, CancellationToken ct);
    }
}