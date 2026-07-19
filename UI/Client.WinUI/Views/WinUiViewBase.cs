using FAP.Application.Views;
using Microsoft.UI.Xaml.Controls;

namespace Fap.Client.WinUI.Views;

public class WinUiViewBase : UserControl, IView
{
    public object? DataContext
    {
        get => base.DataContext;
        set => base.DataContext = value;
    }
}
