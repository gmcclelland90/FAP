using FAP.Shared.Entities;
using FAP.Shared.Interfaces;

namespace FAP.Domain.Verbs
{
    /// <summary>
    /// NOOP verb for keeping connections alive
    /// </summary>
    public class NoopVerb : BaseVerb, FAP.Shared.Interfaces.IVerb
    {
        public string SourceID { set; get; }
        public string AuthKey { set; get; }

        #region IVerb Members

        public NetworkRequest CreateRequest()
        {
            var req = new NetworkRequest();
            req.Data = Serialize(this);
            req.Verb = "NOOP";
            req.SourceID = SourceID;
            req.AuthKey = AuthKey;
            return req;
        }

        public NetworkRequest ProcessRequest(NetworkRequest r)
        {
            ReceiveResponse(r);
            return CreateRequest();
        }

        public bool ReceiveResponse(NetworkRequest r)
        {
            var inc = Deserialise<NoopVerb>(r.Data);
            SourceID = inc.SourceID;
            AuthKey = inc.AuthKey;
            return true;
        }

        #endregion
    }
} 