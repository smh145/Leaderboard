namespace Leaderboard.Api.Controllers.Models
{
    class BucketNode : IDisposable
    {
        public SortedSet<CustomerEntry> CustomerSet { get; }

        /// <summary>
        /// The total number of users with a higher ranking than this bucket.
        /// </summary>
        public int PrefixRank { get; set; }
        public readonly ReaderWriterLockSlim RwLock;

        public BucketNode()
        {
            PrefixRank = 0;
            CustomerSet = new SortedSet<CustomerEntry>();
            RwLock = new ReaderWriterLockSlim();
        }

        public void Dispose()
        {
            RwLock?.Dispose();
        }
    }
}