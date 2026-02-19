#region Copyright Kayomani 2011.  Licensed under the GPLv3 (Or later version), Expand for details. Do not remove this notice.

/**
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or any 
    later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
 * */

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Waf.Applications;
using System.Waf.Applications.Services;
using FAP.Application.ViewModels;
using FAP.Domain.Entities;
using FAP.Domain.Net;
using FAP.Domain.Services;
using FAP.Domain.Verbs;
using Fap.Foundation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FAP.Application.Controllers
{
    public class ConversationController : IConversationController
    {
        private readonly IServiceProvider serviceProvider;
        private readonly Microsoft.Extensions.Logging.ILogger<ConversationController> logger;
        private readonly System.Net.Http.IHttpClientFactory _httpClientFactory;
        private readonly Microsoft.Extensions.Logging.ILogger<ModernHttpClient> _httpLogger;
        private readonly Model model;
        private ConversationViewModel viewModel;

        public ConversationController(IServiceProvider serviceProvider, Model m)
        {
            logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ConversationController>>();
            _httpClientFactory = serviceProvider.GetRequiredService<System.Net.Http.IHttpClientFactory>();
            _httpLogger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ModernHttpClient>>();
            model = m;
            this.serviceProvider = serviceProvider;
        }

        public ConversationViewModel ViewModel
        {
            get { return viewModel; }
        }

        public void Initialize()
        {
            if (null == viewModel)
            {
                viewModel = serviceProvider.GetRequiredService<ConversationViewModel>();
                viewModel.SendChatMessage = new DelegateCommand(SendMessage);
                viewModel.Close = new DelegateCommand(Clear);
            }
        }

        private async void SendMessage()
        {
            if (viewModel?.Conversation?.OtherParty == null)
            {
                logger.LogWarning("No conversation or other party available");
                return;
            }

            if (string.IsNullOrEmpty(viewModel.CurrentChatMessage))
            {
                logger.LogDebug("No message to send");
                return;
            }

            try
            {
                // Add the message to the conversation
                var message = $"You: {viewModel.CurrentChatMessage}";
                viewModel.Conversation.Messages.Add(message);
                
                // Send the direct conversation message via network (1:1)
                var convoVerb = new ConversationVerb();
                convoVerb.Message = viewModel.CurrentChatMessage;
                convoVerb.Nickname = model.Nickname;
                // SourceID is stamped by transport headers
                
                var client = new ModernHttpClient(model.LocalNode, _httpLogger, _httpClientFactory.CreateClient("FapDefault"));
                var ok = await client.ExecuteAsync(convoVerb, viewModel.Conversation.OtherParty);
                if (ok)
                {
                    logger.LogDebug("Message sent to {Nickname}: {Message}", viewModel.Conversation.OtherParty.Nickname, viewModel.CurrentChatMessage);
                    FAP.Shared.FapMetrics.Inc(ref FAP.Shared.FapMetrics.ConversationSent);
                }
                else
                {
                    logger.LogWarning("Failed to send message");
                }
                
                // Clear the input field
                viewModel.CurrentChatMessage = string.Empty;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error sending message");
            }
        }

        private void Clear()
        {
            // Implementation for clearing conversation
            logger.LogDebug("Clearing conversation");
        }

        public bool HandleMessage(string id, string nickname, string message)
        {
            try
            {
                logger.LogDebug("Received message from {Nickname}: {Message}", nickname, message);
                
                // Find the conversation with this user
                if (viewModel?.Conversation?.OtherParty?.ID == id)
                {
                    // Add the received message to the conversation
                    var receivedMessage = $"{nickname}: {message}";
                    viewModel.Conversation.Messages.Add(receivedMessage);
                    logger.LogDebug("Added message to conversation: {ReceivedMessage}", receivedMessage);
                }
                else
                {
                    logger.LogDebug("Message from unknown user {Id} ({Nickname})", id, nickname);
                }
                
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error handling message");
                return false;
            }
        }

        public void CreateConversation(Node peer)
        {
            logger.LogDebug("Creating conversation with peer: {Nickname}", peer.Nickname);
            
            // Create a new conversation
            var conversation = new Conversation();
            conversation.OtherParty = peer;
            
            // Create the conversation view model and set it as the current view model
            viewModel = serviceProvider.GetRequiredService<ConversationViewModel>();
            viewModel.Conversation = conversation;
            viewModel.SendChatMessage = new DelegateCommand(SendMessage);
            viewModel.Close = new DelegateCommand(Clear);
            
            // Get the popup controller and add the conversation window
            var popupController = serviceProvider.GetRequiredService<IPopupWindowController>();
            popupController.AddWindow(viewModel.View, $"Chat with {peer.Nickname}");
        }
    }
}