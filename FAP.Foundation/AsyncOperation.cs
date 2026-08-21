using System;

namespace Fap.Foundation
{
    public class AsyncOperation
    {
        public required Action<object?> Command { get; set; }
        public object? Object { get; set; }
        public Action<object?>? CompletedCommand { get; set; }
    }
}
