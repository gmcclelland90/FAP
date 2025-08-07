using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FAP.Domain.Entities;

namespace FAP.Domain.Services
{
    public class UpdateCheckerService
    {
        private readonly Model model;

        public UpdateCheckerService(Model m)
        {
            model = m;
        }

        public void Run()
        {
            ThreadPool.QueueUserWorkItem(doCheck);
        }

        private void doCheck(object? o)
        {
            try
            {
                using var client = new HttpClient();
                string message = client.GetStringAsync("http://iownallyourbase.com/fap/updates.php?i=" + model.LocalNode.ID + "&v=" +
                                          Model.AppVersion).Result;
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