using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FAP.Application.Services;
using Microsoft.Extensions.Logging;

namespace Server.Console
{
    public class MessageService : IMessageService
    {
        private readonly ILogger<MessageService> logger;

        public MessageService(ILogger<MessageService> logger)
        {
            this.logger = logger;
        }

        public void ShowMessage(string message)
        {
            logger.LogInformation("{Message}", message);
        }

        public void ShowWarning(string message)
        {
            logger.LogWarning("{Message}", message);
        }

        public void ShowError(string message)
        {
           logger.LogError("{Message}", message);
        }
    }
}
