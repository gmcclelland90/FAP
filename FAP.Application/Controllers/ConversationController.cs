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
using NLog;

namespace FAP.Application.Controllers
{
    public class ConversationController : IConversationController
    {
        private readonly IServiceProvider serviceProvider;
        private readonly Logger logger;
        private readonly Model model;
        private ConversationViewModel viewModel;

        public ConversationController(IServiceProvider serviceProvider, Model m)
        {
            logger = LogManager.GetLogger("faplog");
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

        private void SendMessage()
        {
            if (viewModel?.Conversation?.OtherParty == null)
            {
                logger.Warn("No conversation or other party available");
                return;
            }

            if (string.IsNullOrEmpty(viewModel.CurrentChatMessage))
            {
                logger.Debug("No message to send");
                return;
            }

            try
            {
                // Add the message to the conversation
                var message = $"You: {viewModel.CurrentChatMessage}";
                viewModel.Conversation.Messages.Add(message);
                
                // Send the message via network
                var chatVerb = new ChatVerb();
                chatVerb.Message = viewModel.CurrentChatMessage;
                chatVerb.Nickname = model.Nickname;
                chatVerb.SourceID = model.LocalNode.ID;
                
                var client = new ModernHttpClient(model.LocalNode);
                if (client.ExecuteAsync(chatVerb, viewModel.Conversation.OtherParty).Result)
                {
                    logger.Debug($"Message sent to {viewModel.Conversation.OtherParty.Nickname}: {viewModel.CurrentChatMessage}");
                }
                else
                {
                    logger.Warn("Failed to send message");
                }
                
                // Clear the input field
                viewModel.CurrentChatMessage = string.Empty;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error sending message");
            }
        }

        private void Clear()
        {
            // Implementation for clearing conversation
            logger.Debug("Clearing conversation");
        }

        public bool HandleMessage(string id, string nickname, string message)
        {
            try
            {
                logger.Debug($"Received message from {nickname}: {message}");
                
                // Find the conversation with this user
                if (viewModel?.Conversation?.OtherParty?.ID == id)
                {
                    // Add the received message to the conversation
                    var receivedMessage = $"{nickname}: {message}";
                    viewModel.Conversation.Messages.Add(receivedMessage);
                    logger.Debug($"Added message to conversation: {receivedMessage}");
                }
                else
                {
                    logger.Debug($"Message from unknown user {id} ({nickname})");
                }
                
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error handling message");
                return false;
            }
        }

        public void CreateConversation(Node peer)
        {
            logger.Debug($"Creating conversation with peer: {peer.Nickname}");
            
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