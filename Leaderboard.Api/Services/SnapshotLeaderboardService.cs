using Leaderboard.Api.Controllers.Leaderboard.Dtos;
using Leaderboard.Api.Interfaces;
using System.Collections.Concurrent;

namespace Leaderboard.Api.Services
{
    /// <summary>
    /// Snapshot leaderboard implementation using background task and buffering.
    /// This leaderboard service achieves more efficient score updates and rank queries by buffering score changes in a queue 
    /// and periodically processing these updates via a background task to build leaderboard snapshots.
    /// 1. When updating scores, the service adds update requests to the queue and immediately returns the latest score to the caller.
    /// 2. Reduces write lock hold time, improving concurrent processing capability.
    /// 3. During periodic leaderboard rebuilds, merges score change operations for the same user, reducing the number of sorts.
    /// 4. Reduces the frequency of cache rebuilds.
    /// However, this may result in situations where score updates succeed but rank queries do not yet reflect the latest scores.
    /// </summary>
    public class SnapshotLeaderboardService : ILeaderboardService
    {
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        /// <summary>
        /// Realtime Stores the score for each customer. customerId => score
        /// Only used for score retrieval without waiting
        /// </summary>
        private readonly ConcurrentDictionary<long, long> _realtimeCustomerScores;

        /// <summary>
        /// Stores the score for each customer. customerId => score
        /// Used for building immutable snapshots
        /// </summary>
        private readonly ConcurrentDictionary<long, long> _customerScores;

        /// <summary> 
        /// Dictionary is sorted by score bucket in descending order.   
        /// Whether to bucket by individual score or by score ranges (10,100,1000... ) 
        /// should be determined by business requirements. For now, bucketing by 100.
        /// Bucket key score / 100 => a sorted set of CustomerEntry.
        /// Within each bucket, entries are sorted by score descending, then by customerId ascending.
        /// </summary>
        private readonly SortedDictionary<long, SortedSet<CustomerEntry>> _scoreBuckets;

        /// <summary>
        /// Cache stores prefix sums for fast rank lookups.
        /// - count: The number of customers with a higher rank (its starting rank)
        /// - bucketKey: The bucket key for the range.
        /// - bucket: The set of CustomerEntry in that bucket.
        /// </summary>
        private List<(int count, long bucketKey, SortedSet<CustomerEntry> bucket)> _prefixSums;
        /// <summary>
        /// Cache bucketKey => starting rank
        /// </summary>
        private Dictionary<long, int> _prefixSumsByScore;
        /// <summary>
        /// Cache customerId => rank
        /// </summary>
        private Dictionary<long, int> _rankByCustomerId;
        /// <summary>
        /// Bucket size for score ranges
        /// </summary>
        private const int BUCKET_SIZE = 100;

        /// <summary>
        /// Updates queue
        /// </summary>
        private readonly ConcurrentQueue<ScoreUpdate> _updateQueue;
        /// <summary>
        /// Updates queue Count
        /// </summary>
        private int _updateQueueCount;

        /// <summary>
        /// Rebuilds interval
        /// </summary>
        private const int TIME_SLICE_MS = 100;

        /// <summary>
        /// Background task for rebuilding
        /// </summary>
        private readonly Task _rebuildTask;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public SnapshotLeaderboardService()
        {
            _realtimeCustomerScores = new ConcurrentDictionary<long, long>();
            _customerScores = new ConcurrentDictionary<long, long>();
            _scoreBuckets = new SortedDictionary<long, SortedSet<CustomerEntry>>(
                Comparer<long>.Create((a, b) => b.CompareTo(a)));

            _prefixSums = new List<(int count, long bucketKey, SortedSet<CustomerEntry> bucket)>();
            _prefixSumsByScore = new Dictionary<long, int>();
            _rankByCustomerId = new Dictionary<long, int>();

            _updateQueue = new ConcurrentQueue<ScoreUpdate>();
            _updateQueueCount = 0;

            _cancellationTokenSource = new CancellationTokenSource();
            _rebuildTask = Task.Run(() => RebuildLoop(_cancellationTokenSource.Token));
        }

        private int _isRebuilding;

        /// <summary>
        /// Periodically rebuilds
        /// </summary>
        private async Task RebuildLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TIME_SLICE_MS, cancellationToken);

                    int pendingCount = Interlocked.CompareExchange(ref _updateQueueCount, 0, 0);
                    if (pendingCount > 0 && Interlocked.CompareExchange(ref _isRebuilding, 1, 0) == 0)
                    {
                        // Only start rebuild if not already rebuilding
                        try
                        {
                            await ProcessRebuildAsync();
                        }
                        finally
                        {
                            Interlocked.Exchange(ref _isRebuilding, 0);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception)
                {
                }
            }
        }

        /// <summary>
        /// Process rebuild
        /// </summary>
        private Task ProcessRebuildAsync()
        {
            _lock.EnterWriteLock();
            try
            {
                Dictionary<long, long> customerScoreDelta = new Dictionary<long, long>();
                while (_updateQueue.TryDequeue(out var update))
                {
                    Interlocked.Decrement(ref _updateQueueCount);
                    if (!customerScoreDelta.ContainsKey(update.CustomerId))
                    {
                        customerScoreDelta.Add(update.CustomerId, 0);
                    }

                    customerScoreDelta[update.CustomerId] += update.ScoreDelta;
                }

                foreach (var item in customerScoreDelta)
                {
                    long customerId = item.Key;
                    long newScore = item.Value;
                    if (_customerScores.TryGetValue(customerId, out long curScore))
                    {
                        newScore = curScore + item.Value;
                        // Remove the customer from old bucket.
                        long oldBucketKey = GetBucketKey(curScore);
                        if (_scoreBuckets.TryGetValue(oldBucketKey, out var oldBucket))
                        {
                            oldBucket.Remove(new CustomerEntry(curScore, customerId));
                            if (oldBucket.Count == 0)
                            {
                                _scoreBuckets.Remove(oldBucketKey);
                            }
                        }
                    }

                    _customerScores[customerId] = newScore;

                    // Add the customer to new bucket.
                    long newBucketKey = GetBucketKey(newScore);
                    if (!_scoreBuckets.TryGetValue(newBucketKey, out var newBucket))
                    {
                        newBucket = new SortedSet<CustomerEntry>();
                        _scoreBuckets[newBucketKey] = newBucket;
                    }
                    newBucket.Add(new CustomerEntry(newScore, customerId));
                }

                RebuildCache();
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            return Task.CompletedTask;
        }


        public Task<long> UpdateScoreAsync(long customerId, long score)
        {
            long newScore = _realtimeCustomerScores.AddOrUpdate(
                customerId,
                score,
                (key, existingScore) => existingScore + score);

            var update = new ScoreUpdate(customerId, score);
            _updateQueue.Enqueue(update);
            Interlocked.Increment(ref _updateQueueCount);

            return Task.FromResult(newScore);
        }

        public Task<List<CustomerRankInfo>> GetRanksAsync(int start, int end)
        {
            _lock.EnterReadLock();

            try
            {
                if (end < start || _customerScores.Count < start)
                {
                    return Task.FromResult(new List<CustomerRankInfo>());

                }

                return GetRanksByRangeAsync(start, end);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public Task<List<CustomerRankInfo>> GetRanksByIdAsync(long customerId, int high, int low)
        {
            _lock.EnterReadLock();

            try
            {
                var result = new List<CustomerRankInfo>();
                if (!_customerScores.TryGetValue(customerId, out var customerScore))
                {
                    return Task.FromResult(result);
                }

                long bucketKey = GetBucketKey(customerScore);
                if (!_scoreBuckets.TryGetValue(bucketKey, out var bucket))
                {
                    return Task.FromResult(result);
                }

                // Calculate customer's rank.
                int rankInBucket = 0;
                foreach (var entry in bucket)
                {
                    if (entry.Score > customerScore || (entry.Score == customerScore && entry.CustomerId < customerId))
                    {
                        rankInBucket++;
                    }
                    else if (entry.CustomerId == customerId)
                    {
                        rankInBucket++;
                        break;
                    }
                }

                int customerRank = _prefixSumsByScore[bucketKey] + rankInBucket;

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
                _lock.ExitReadLock();
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
                var bucketKey = _prefixSums[bucketIdx].bucketKey;
                var entries = _prefixSums[bucketIdx].bucket;

                if (bucketKey < 0)
                {
                    break;
                }

                foreach (var entry in entries)
                {
                    if (curRank >= start && curRank <= end)
                    {
                        result.Add(new CustomerRankInfo
                        {
                            CustomerId = entry.CustomerId,
                            Score = entry.Score,
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

        #region Utility function
        /// <summary>
        /// Calculate the bucket key for a given score
        /// Scores are bucketed by ranges of BUCKET_SIZE
        /// </summary>
        private long GetBucketKey(long score)
        {
            return score >= 0 ? score / BUCKET_SIZE : (score - 99) / BUCKET_SIZE;
        }

        /// <summary>
        /// Binary search to find the index of the score bucket that contains the target rank
        /// </summary>
        /// <param name="targetRank">The target rank (1-based)</param>
        /// <returns>The index of the score bucket in the _prefixSums list</returns>
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
        /// Rebuilds cache
        /// </summary>
        private void RebuildCache()
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
        }
        #endregion 

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();

            try
            {
                _rebuildTask?.Wait(TimeSpan.FromSeconds(10));
            }
            catch (Exception)
            {
            }
            _lock?.Dispose();
        }
    }
}