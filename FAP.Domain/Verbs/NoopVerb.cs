using FAP.Shared.Entities;
using FAP.Shared.Interfaces;

namespace FAP.Domain.Verbs
{
    /// <summary>
    /// NOOP verb for keeping connections alive
    /// </summary>
    public class NoopVerb : BaseVerb, FAP.Shared.Interfaces.IVerb
    {
        public string SourceID { set; get; } = string.Empty;
        public string AuthKey { set; get; } = string.Empty;

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
            // Overlord NOOP replies are empty HTTP 200s (no JSON body). Deserializing
            // that used to throw, making ExecuteAsync return false and the client
            // disconnect/reconnect every UPLINK_TIMEOUT (~60s).
            if (string.IsNullOrWhiteSpace(r.Data))
                return true;

            var inc = Deserialise<NoopVerb>(r.Data);
            if (inc == null)
                return true;

            SourceID = inc.SourceID;
            AuthKey = inc.AuthKey;
            return true;
        }

        #endregion
    }
} 