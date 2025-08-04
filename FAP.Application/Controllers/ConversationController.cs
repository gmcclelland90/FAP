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
            // Implementation for sending message
            logger.Debug("Sending message");
        }

        private void Clear()
        {
            // Implementation for clearing conversation
            logger.Debug("Clearing conversation");
        }

        public bool HandleMessage(string id, string nickname, string message)
        {
            // TODO: Implement actual message handling logic
            return true;
        }

        public void CreateConversation(Node peer)
        {
            // TODO: Implement conversation creation logic
            logger.Debug($"Creating conversation with peer: {peer.Nickname}");
        }
    }
}