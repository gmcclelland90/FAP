using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace FAP.Domain.Services
{
    public class StreamingJsonService
    {
        public async IAsyncEnumerable<T> DeserializeStreamAsync<T>(Stream stream)
        {
            await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable<T>(stream))
            {
                if (item is not null)
                {
                    yield return item;
                }
            }
        }

        public async Task SerializeStreamAsync<T>(Stream stream, IAsyncEnumerable<T> items)
        {
            await JsonSerializer.SerializeAsync(stream, items);
        }
    }
}


