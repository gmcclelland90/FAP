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
using FAP.Domain.Services;
using Fap.Foundation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FAP.Application.Controllers
{
    public class CompareController : AsyncControllerBase
    {
        private readonly IServiceProvider serviceProvider;
        private readonly Microsoft.Extensions.Logging.ILogger<CompareController> logger;
        private readonly Model model;
        private CompareViewModel viewModel;

        public CompareController(IServiceProvider serviceProvider, Model m)
        {
            logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CompareController>>();
            model = m;
            this.serviceProvider = serviceProvider;
        }

        public CompareViewModel ViewModel
        {
            get { return viewModel; }
        }

        public CompareViewModel Initalise()
        {
            if (null == viewModel)
            {
                viewModel = serviceProvider.GetRequiredService<CompareViewModel>();
                viewModel.Run = new DelegateCommand(Compare);
                viewModel.Reset = new DelegateCommand(Reset);
            }
            return viewModel;
        }

        private void Compare()
        {
            // Implementation for compare functionality
            logger.LogDebug("Compare operation started");
        }

        private void Reset()
        {
            // Implementation for reset functionality
            logger.LogDebug("Reset operation started");
        }
    }
}