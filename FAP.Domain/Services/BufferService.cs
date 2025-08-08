using System.Collections.Generic;
using Fap.Foundation;
using Microsoft.Extensions.Logging;

namespace FAP.Domain.Services
{
    public class BufferService
    {
        public const int Buffer = 2621440; //2mb
        public const int SmallBuffer = 25600; //25kb
        private readonly ILogger<BufferService> logger;
        private readonly Stack<MemoryBuffer> pool = new Stack<MemoryBuffer>();
        private readonly Stack<MemoryBuffer> smallPool = new Stack<MemoryBuffer>();

        private int largeCount = 10;
        private int smallCount = 10;

        public BufferService(ILogger<BufferService> logger)
        {
            this.logger = logger;
        }

        public void Clean()
        {
            lock (pool)
            {
                while (pool.Count > largeCount)
                {
                    MemoryBuffer arg = pool.Pop();
                    arg.Dispose();
                }
            }
            lock (smallPool)
            {
                while (smallPool.Count > smallCount)
                {
                    MemoryBuffer arg = smallPool.Pop();
                    arg.Dispose();
                }
            }
        }

        /* Do not prepopulate
         * public void Setup(int smallCount, int largeCount)
        {
            lock (smallPool)
            {
                this.smallCount = smallCount;
                for (int i = 0; i < smallCount; i++)
                {
                    MemoryBuffer a = new MemoryBuffer(SmallBuffer);
                    smallPool.Push(a);
                }
            }
            lock (pool)
            {
                this.largeCount = largeCount;
                for (int i = 0; i < largeCount; i++)
                {
                    MemoryBuffer a = new MemoryBuffer(Buffer);
                    pool.Push(a);
                }
            }
        }*/


        public MemoryBuffer GetBuffer()
        {
            lock (pool)
            {
                if (pool.Count > 0)
                    return pool.Pop();
            }
            var a = new MemoryBuffer(Buffer);
            return a;
        }

        public MemoryBuffer GetSmallBuffer()
        {
            lock (smallPool)
            {
                if (smallPool.Count > 0)
                    return smallPool.Pop();
            }
            var a = new MemoryBuffer(SmallBuffer);
            return a;
        }

        public void FreeBuffer(MemoryBuffer input)
        {
            input.SetDataLocation(0, 0);
            if (input.Data.Length == SmallBuffer)
            {
                lock (smallPool)
                    smallPool.Push(input);
            }
            else if (input.Data.Length == Buffer)
            {
                lock (pool)
                    pool.Push(input);
            }
            else
            {
                logger.LogWarning("BufferService.FreeBuffer: Unexpected buffer size {Size}", input.Data.Length);
            }
        }
    }
}