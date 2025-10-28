using Leaderboard.Api.Controllers.Leaderboard.Dtos;
using Leaderboard.Api.Interfaces;

namespace Leaderboard.Api.Services
{
    public class LeaderboardService : ILeaderboardService
    {
        /// <summary>
        /// ReaderWriterLockSlim does not strictly guarantee FIFO on the server side.
        /// If a fair reader-writer lock could be implemented, further optimizations are possible (I failed):
        /// 1. For an update request, the current score can be returned immediately, while the Leaderboard update operation waits for the lock.
        /// 2. Consecutive write requests can be merged. Updates for the same customerId can be combined, reducing the number of write lock acquisitions.
        /// 3. Rebuild the index when switching between read and write locks.
        /// </summary>
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        /// <summary>
        /// Stores the score for each customer. customerId => score
        /// </summary>
        private readonly Dictionary<long, long> _customerScores;

        /// <summary> 
        /// Dictionary is sorted by score in descending order.   
        /// Whether to bucket by individual score or by score ranges (10,100,1000... ) 
        /// should be determined by business requirements. For now, bucketing by individual score.
        /// Score => a sorted set of customerIds.
        /// </summary>
        private readonly SortedDictionary<long, SortedSet<long>> _scoreBuckets;

        /// <summary>
        /// Cache stores prefix sums for fast rank lookups.
        /// - count: The number of customers with a higher score.(its starting rank)
        /// - score: The score for the bucket.
        /// - bucket: The set of customer IDs with that score.
        /// </summary>
        private List<(int count, long score, SortedSet<long> bucket)> _prefixSums;
        /// <summary>
        /// Cache score => starting rank
        /// </summary>
        private Dictionary<long, int> _prefixSumsByScore;
        /// <summary>
        /// Cache customerId => rank
        /// </summary>
        private Dictionary<long, int> _rankByCustomerId;
        /// <summary>
        /// A flag whether caches are dirty.
        /// </summary>
        private bool _isDirty;

        public LeaderboardService()
        {
            _customerScores = new Dictionary<long, long>();
            _scoreBuckets = new SortedDictionary<long, SortedSet<long>>(Comparer<long>.Create((a, b) => b.CompareTo(a)));

            _prefixSums = new List<(int count, long score, SortedSet<long> bucket)>();
            _prefixSumsByScore = new Dictionary<long, int>();
            _rankByCustomerId = new Dictionary<long, int>();
            _isDirty = true;
        }

        public Task<long> UpdateScoreAsync(long customerId, long score)
        {
            _lock.EnterWriteLock();
            try
            {
                long newScore = score;
                if (_customerScores.TryGetValue(customerId, out long curScore))
                {
                    newScore = curScore + score;
                    // Remove the customer from old bucket.
                    if (_scoreBuckets.TryGetValue(curScore, out var oldBucket))
                    {
                        oldBucket.Remove(customerId);
                        if (oldBucket.Count == 0)
                        {
                            _scoreBuckets.Remove(curScore);
                        }
                    }
                }

                _customerScores[customerId] = newScore;


                // Add the customer to new bucket.
                if (!_scoreBuckets.TryGetValue(newScore, out var newBucket))
                {
                    newBucket = new SortedSet<long>();
                    _scoreBuckets[newScore] = newBucket;
                }
                newBucket.Add(customerId);

                // Mark cache dirty, rebuilt next read
                _isDirty = true;

                return Task.FromResult(newScore);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Binary search to find the index of the score bucket that contains the target rank.
        /// </summary>
        /// <param name="targetRank">The target rank (1-based).</param>
        /// <returns>The index of the score bucket in the _prefixSums list.</returns>
        private int BinarySearchBucket(int targetRank)
        {
            int left = 0;
            int right = _prefixSums.Count - 1;
            int result = 0;

            while (left <= right)
            {
                int mid = left + (right - left) / 2;

                int startRank = _prefixSums[mid].count + 1;
                int endRank = mid == _prefixSums.Count - 1 ? _customerScores.Count : _prefixSums[mid + 1].count;

                if (startRank <= targetRank && targetRank <= endRank)
                {
                    return mid;
                }
                else if (targetRank < startRank)
                {
                    right = mid - 1;
                }
                else
                {
                    left = mid + 1;
                    result = mid + 1;
                }
            }

            return result;
        }

        /// <summary>
        /// Rebuilds prefix
        /// </summary>
        private void RebuildPrefixSums()
        {
            if (_isDirty)
            {
                _lock.EnterWriteLock();
                try
                {
                    // Double-check the _isDirty flag
                    if (_isDirty)
                    {
                        int count = 0;
                        _prefixSums.Clear();
                        _prefixSumsByScore.Clear();
                        _rankByCustomerId.Clear();

                        foreach (var bucket in _scoreBuckets)
                        {
                            _prefixSums.Add((count, bucket.Key, bucket.Value));
                            _prefixSumsByScore.Add(bucket.Key, count);
                            count += bucket.Value.Count;
                        }

                        _isDirty = false;
                    }
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
        }

        public Task<List<CustomerRankInfo>> GetRanksAsync(int start, int end)
        {
            _lock.EnterUpgradeableReadLock();

            try
            {
                RebuildPrefixSums();

                return GetRanksByRangeAsync(start, end);
            }
            finally
            {
                _lock.ExitUpgradeableReadLock();
            }
        }

        private Task<List<CustomerRankInfo>> GetRanksByRangeAsync(int start, int end)
        {
            var result = new List<CustomerRankInfo>();
            if (_prefixSums.Count == 0)
            {
                return Task.FromResult(result);
            }


            // Use binary search to find the starting bucket.
            int startBucketIndex = BinarySearchBucket(start);
            int curRank = _prefixSums[startBucketIndex].count + 1;

            for (int bucketIdx = startBucketIndex; bucketIdx < _prefixSums.Count; bucketIdx++)
            {
                var score = _prefixSums[bucketIdx].score;
                var customerIds = _prefixSums[bucketIdx].bucket;

                if (score <= 0)
                {
                    break;
                }

                foreach (var customerId in customerIds)
                {
                    if (curRank >= start && curRank <= end)
                    {
                        result.Add(new CustomerRankInfo
                        {
                            CustomerId = customerId,
                            Score = score,
                            Rank = curRank
                        });

                        if (curRank == end)
                        {
                            return Task.FromResult(result);
                        }
                    }

                    curRank++;
                }

                if (curRank > end)
                {
                    break;
                }
            }

            return Task.FromResult(result);
        }

        public Task<List<CustomerRankInfo>> GetRanksByIdAsync(long customerId, int high, int low)
        {
            _lock.EnterUpgradeableReadLock();

            try
            {
                RebuildPrefixSums();

                var result = new List<CustomerRankInfo>();
                if (!_customerScores.TryGetValue(customerId, out var customerScore))
                {
                    return Task.FromResult(result);
                }

                if (!_scoreBuckets.TryGetValue(customerScore, out var bucket))
                {
                    return Task.FromResult(result);
                }

                // Calculate customer's rank.
                // The rank is the sum of players in higher-ranked buckets (from _prefixSumsByScore)
                // plus the customer's position within their own score bucket.
                int rankInBucket = bucket.GetViewBetween(bucket.Min, customerId).Count;
                int customerRank = _prefixSumsByScore[customerScore] + rankInBucket;

                if (customerRank == 0)
                {
                    return Task.FromResult(result);
                }
                else
                {
                    int start = Math.Max(1, customerRank - high);
                    int end = Math.Min(_customerScores.Count, customerRank + low);

                    return GetRanksByRangeAsync(start, end);
                }
            }
            finally
            {
                _lock.ExitUpgradeableReadLock();
            }
        }

        public void Dispose()
        {
            _lock?.Dispose();
        }
    }
}
