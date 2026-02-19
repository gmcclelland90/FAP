using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FAP.Domain.Entities;

namespace FAP.Domain.Services
{
    public class UpdateCheckerService
    {
        private readonly Model model;
        private readonly IHttpClientFactory _httpClientFactory;

        public UpdateCheckerService(Model m, IHttpClientFactory httpClientFactory)
        {
            model = m;
            _httpClientFactory = httpClientFactory;
        }

        public void Run()
        {
            _ = Task.Run(() => doCheck(null));
        }

        private async void doCheck(object? o)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("FapDefault");
                string message = await client.GetStringAsync("http://iownallyourbase.com/fap/updates.php?i=" + model.LocalNode.ID + "&v=" +
                                          Model.AppVersion);
                if (null != message)
                {
                    foreach (string split in message.Split('\n'))
                        model.Messages.Add(split);
                }
            }
            catch
            {
                model.Messages.Add("An error occured during client update check");
            }
        }
    }
}