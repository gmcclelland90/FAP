using FAP.Shared.Entities;
using FAP.Shared.Interfaces;

namespace FAP.Domain.Verbs
{
    public class AddDownload : BaseVerb, FAP.Shared.Interfaces.IVerb
    {
        public string URL { set; get; } = string.Empty;

        #region IVerb Members

        public NetworkRequest CreateRequest()
        {
            var req = new NetworkRequest();
            req.Verb = "ADDDOWNLOAD";
            req.Param = URL;
            return req;
        }

        public NetworkRequest ProcessRequest(NetworkRequest r)
        {
            URL = r.Param;
            return new NetworkRequest();
        }

        public bool ReceiveResponse(NetworkRequest r)
        {
            return true;
        }

        #endregion
    }
}