using System;

namespace FAP.Domain.Entities
{
    public class LightweightTransferWorker : ITransferWorker
    {
        public long Length { get; set; }
        public bool IsComplete { get; set; }
        public long Speed { get; set; }
        public string Status { get; set; } = "Preparing upload";
        public long Position { get; set; }

        public DateTime TransferStart { get; set; } = DateTime.Now;
        public long ResumePoint { get; set; } = 0;
    }
}


