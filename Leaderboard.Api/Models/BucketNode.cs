using Leaderboard.Api.DataStructures;
using System.Collections.Concurrent;

namespace Leaderboard.Api.Controllers.Models
{
    class BucketNode : IDisposable
    {

        /// <summary>
        /// The total number of users with a higher ranking than this bucket.
        /// </summary>
        public int PrefixRank { get; set; }

        public readonly ReaderWriterLockSlim RwLock;

        //public SortedSet<CustomerEntry> CustomerSet { get; }
        public SkipList<CustomerEntry> CustomerSet { get; }

        public BucketNode()
        {
            PrefixRank = 0;
            //CustomerSet = new SortedSet<CustomerEntry>();
            CustomerSet = new SkipList<CustomerEntry>();
            RwLock = new ReaderWriterLockSlim();
        }

        public void Dispose()
        {
            RwLock?.Dispose();
        }
    }
}