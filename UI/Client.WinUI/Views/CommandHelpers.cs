using System.Windows.Input;

namespace Fap.Client.WinUI.Views;

internal static class CommandHelpers
{
    public static void TryExecute(ICommand? command, object? parameter = null)
    {
        if (command?.CanExecute(parameter) == true)
            command.Execute(parameter);
    }
}
