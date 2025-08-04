using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FAP.Application.Views;
using FAP.Application.ViewModels;
using FAP.Application.ViewModel;
using FAP.Application.Controllers;
using FAP.Domain.Services;
using Fap.Presentation.Panels;
using System.Waf.Presentation.Services;
using System.Waf.Applications.Services;

namespace Fap.Presentation
{
    // This module is no longer needed as we're using Microsoft.Extensions.DependencyInjection
    // Services are now registered directly in App.xaml.cs
    public class GUIModule
    {
        // Migration note: This class is kept for reference but is no longer used
        // All service registration is now done in App.xaml.cs using IServiceCollection
    }
}
