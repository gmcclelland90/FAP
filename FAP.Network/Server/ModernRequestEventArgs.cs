using System;

namespace FAP.Network.Server
{
    /// <summary>
    /// A request have been received.
    /// </summary>
    public class RequestEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RequestEventArgs"/> class.
        /// </summary>
        /// <param name="request">Received request.</param>
        /// <param name="response">Response to send.</param>
        /// <param name="context">HTTP context.</param>
        public RequestEventArgs(ModernHttpRequest request, ModernHttpResponse response, ModernHttpContext context)
        {
            Request = request;
            Response = response;
            Context = context;
            IsHandled = false;
        }

        /// <summary>
        /// Gets or sets if the request have been handled.
        /// </summary>
        /// <remarks>
        /// The library will not attempt to send the response object
        /// back to the client if this property is set to <c>true</c>.
        /// </remarks>
        public bool IsHandled { get; set; }

        /// <summary>
        /// Gets request object.
        /// </summary>
        public ModernHttpRequest Request { get; private set; }

        /// <summary>
        /// Gets response object.
        /// </summary>
        public ModernHttpResponse Response { get; private set; }

        /// <summary>
        /// Gets context that received the request.
        /// </summary>
        public ModernHttpContext Context { get; private set; }
    }
} 