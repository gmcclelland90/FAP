using System.Windows.Input;

namespace Fap.Foundation
{
    public class AsyncOperation
    {
        public required ICommand Command { set; get; }
        public object? Object { set; get; }
        public ICommand? CompletedCommand { set; get; }
    }
}
