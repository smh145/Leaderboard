using Leaderboard.Api.Controllers.Models;
using Leaderboard.Api.Extensions;
using Leaderboard.Api.Interfaces;
using System.Collections.Concurrent;

namespace Leaderboard.Api.Services
{
    /// <summary>
    /// Leaderboard
    /// </summary>
    public class LeaderboardService : ILeaderboardService, IDisposable
    {
        /// <summary>
        /// Stores the score for each customer.
        /// CustomerId => score
        /// </summary>
        private readonly ConcurrentDictionary<long, long> _customerScores;

        /// <summary> 
        /// Buckets are divided by score range, 
        /// and the bucket size should be adjusted according to actual business scenarios.
        /// Buckets for low score ranges should be smaller to distribute write operations, 
        /// but it's better to keep them larger than 2000 to reduce cross-bucket operations.
        /// Within each bucket, entries are sorted in descending order of score, then in ascending order of customerId.
        /// </summary>
        private readonly Dictionary<long, BucketNode> _scoreBuckets;

        public LeaderboardService()
        {
            _customerScores = new ConcurrentDictionary<long, long>();
            _scoreBuckets = new Dictionary<long, BucketNode>();

            for (long i = 0; i <= BUCKET_MAX_SIZE; i++)
            {
                _scoreBuckets.Add(i, new BucketNode());
            }
        }

        private const int BUCKET_MAX_SIZE = 31;
        private long GetBucketKey(long score)
        {
            if (score <= 0) return 0;

            if (score <= 5000) return 1; if (score <= 10000) return 2; if (score <= 15000) return 3;
            if (score <= 20000) return 4; if (score <= 25000) return 5; if (score <= 30000) return 6;
            if (score <= 35000) return 7; if (score <= 40000) return 8; if (score <= 45000) return 9;

            if (score <= 50000) return 10; if (score <= 100000) return 11; if (score <= 150000) return 12;
            if (score <= 200000) return 13; if (score <= 250000) return 14; if (score <= 300000) return 15;
            if (score <= 350000) return 16; if (score <= 400000) return 17; if (score <= 450000) return 18;

            if (score <= 500000) return 19; if (score <= 1000000) return 20; if (score <= 1500000) return 21;
            if (score <= 2000000) return 22; if (score <= 2500000) return 23; if (score <= 3000000) return 24;
            if (score <= 3500000) return 25; if (score <= 4000000) return 26; if (score <= 4500000) return 27;

            if (score <= 5000000) return 28; if (score <= 10000000) return 29; if (score <= 20000000) return 30;

            return 31;
        }

        public Task<long> UpdateScoreAsync(long customerId, long score)
        {
            long oldScore = long.MinValue;
            long newScore = _customerScores.AddOrUpdate(customerId, score, (key, exScore) =>
            {
                oldScore = exScore;
                return exScore + score;
            });

            if (oldScore == long.MinValue)
            {
                _customerScores.TryGetValue(customerId, out oldScore);
            }

            long oldBucketKey = GetBucketKey(oldScore);
            long newBucketKey = GetBucketKey(newScore);

            // Acquire locks in sequence while updating the prefix sum.
            // Since the bucket size is larger than the score of a single update,
            // the update of the prefix sum only occurs during cross-bucket operations,
            // and only the prefix sum of one bucket needs to be updated each time.
            if (oldBucketKey == newBucketKey)
            {
                if (oldBucketKey != 0)
                {
                    var oldBucket = _scoreBuckets[oldBucketKey];

                    oldBucket.RwLock.WithWriteLock(() =>
                    {
                        oldBucket.CustomerSet.Remove(new CustomerEntry(oldScore, customerId));
                        oldBucket.CustomerSet.Add(new CustomerEntry(newScore, customerId));
                    });
                }
                else
                {
                    // score <= 0, no need to rank
                }
            }
            else if (newBucketKey > oldBucketKey)
            {
                if (oldBucketKey != 0)
                {
                    var oldBucket = _scoreBuckets[oldBucketKey];
                    var newBucket = _scoreBuckets[newBucketKey];

                    // Acquire locks in sequence to prevent deadlocks.
                    newBucket.RwLock.WithWriteLock(() =>
                    {
                        oldBucket.RwLock.WithWriteLock(() =>
                        {
                            oldBucket.CustomerSet.Remove(new CustomerEntry(oldScore, customerId));
                            newBucket.CustomerSet.Add(new CustomerEntry(newScore, customerId));

                            oldBucket.PrefixRank++;
                        });
                    });

                }
                else
                {
                    var newBucket = _scoreBuckets[newBucketKey];
                    newBucket.RwLock.WithWriteLock(() =>
                    {
                        newBucket.CustomerSet.Add(new CustomerEntry(newScore, customerId));
                    });
                }
            }
            else
            {
                // oldBucketKey > newBucketKey
                if (newBucketKey != 0)
                {
                    var oldBucket = _scoreBuckets[oldBucketKey];
                    var newBucket = _scoreBuckets[newBucketKey];

                    // Acquire locks in sequence to prevent deadlocks.
                    oldBucket.RwLock.WithWriteLock(() =>
                    {
                        newBucket.RwLock.WithWriteLock(() =>
                        {
                            oldBucket.CustomerSet.Remove(new CustomerEntry(oldScore, customerId));
                            newBucket.CustomerSet.Add(new CustomerEntry(newScore, customerId));

                            newBucket.PrefixRank--;
                        });
                    });
                }
                else
                {
                    var oldBucket = _scoreBuckets[oldBucketKey];
                    oldBucket.RwLock.WithWriteLock(() =>
                    {
                        oldBucket.CustomerSet.Remove(new CustomerEntry(oldScore, customerId));
                    });
                }
            }

            return Task.FromResult(newScore);
        }

        public Task<List<CustomerRankInfo>> GetRanksAsync(int start, int end)
        {
            var result = new List<CustomerRankInfo>();
            if (end < start)
            {
                return Task.FromResult(result);
            }

            // Acquire locks in sequence to prevent deadlocks.
            // When the next lock is needed, acquire the next lock first before releasing the current one.
            // Moreover, locking in this way prevents the situation where the same user has multiple rankings.
            for (long i = BUCKET_MAX_SIZE; i > 0; i--)
            {
                var bucket = _scoreBuckets[i];
                if (i == BUCKET_MAX_SIZE)
                {
                    bucket.RwLock.EnterReadLock();
                }
                try
                {
                    int curRank = bucket.PrefixRank + 1;
                    int maxRank = bucket.PrefixRank + bucket.CustomerSet.Count;

                    if (end < curRank)
                    {
                        break;
                    }

                    if (maxRank < start)
                    {
                        if (i - 1 > 0)
                        {
                            _scoreBuckets[i - 1].RwLock.EnterReadLock();
                        }
                        continue;
                    }

                    int innerRank = 1;
                    IEnumerable<CustomerEntry> view = bucket.CustomerSet;
                    if (curRank < start)
                    {
                        // O(log N) to find the start
                        innerRank = start - bucket.PrefixRank;
                        view = bucket.CustomerSet.RangeByRank(innerRank, bucket.CustomerSet.Count);
                        curRank = start;
                    }

                    foreach (var entry in view)
                    {
                        if (start <= curRank && curRank <= end)
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
                        innerRank++;
                    }

                    if (i - 1 > 0)
                    {
                        _scoreBuckets[i - 1].RwLock.EnterReadLock();
                    }
                }
                finally
                {
                    bucket.RwLock.ExitReadLock();
                }
            }

            return Task.FromResult(result);
        }

        public Task<List<CustomerRankInfo>> GetRanksByIdAsync(long customerId, int high, int low)
        {
            var result = new List<CustomerRankInfo>();
            if (!_customerScores.TryGetValue(customerId, out var customerScore))
            {
                return Task.FromResult(result);
            }

            var customerBucketKey = GetBucketKey(customerScore);
            if (customerBucketKey <= 0)
            {
                return Task.FromResult(result);
            }

            // Only get [1, CustomerBucket] ReadLock
            for (long i = BUCKET_MAX_SIZE; i > customerBucketKey; i--)
            {
                var bucket = _scoreBuckets[i];
                bucket.RwLock.EnterReadLock();
            }
            var customerBucket = _scoreBuckets[customerBucketKey];
            customerBucket.RwLock.EnterReadLock();

            // Get customer's rank
            int customerRank = customerBucket.PrefixRank + customerBucket.CustomerSet.GetRankByValue(new CustomerEntry(customerScore, customerId));

            int start = Math.Max(1, customerRank - high);
            int end = customerRank + low;

            // Get (CustomerBucket, EndBucket] ReadLock
            // Acquire locks in sequence to prevent deadlocks.
            // When the next lock is needed, acquire the next lock first before releasing the current one.
            // Moreover, locking in this way prevents the situation where the same user has multiple rankings.
            for (long i = BUCKET_MAX_SIZE; i > 0; i--)
            {
                var bucket = _scoreBuckets[i];
                try
                {
                    int curRank = bucket.PrefixRank + 1;
                    int maxRank = bucket.PrefixRank + bucket.CustomerSet.Count;

                    if (end < curRank)
                    {
                        break;
                    }

                    if (maxRank < start)
                    {
                        if (customerBucketKey > i - 1 && i - 1 > 0)
                        {
                            _scoreBuckets[i - 1].RwLock.EnterReadLock();
                        }
                        continue;
                    }

                    int innerRank = 1;
                    IEnumerable<CustomerEntry> view = bucket.CustomerSet;
                    if (curRank < start)
                    {
                        // O(log N) to find the start
                        innerRank = start - bucket.PrefixRank;
                        view = bucket.CustomerSet.RangeByRank(innerRank, bucket.CustomerSet.Count);
                        curRank = start;
                    }

                    foreach (var entry in view)
                    {
                        if (start <= curRank && curRank <= end)
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
                        innerRank++;
                    }

                    if (customerBucketKey > i - 1 && i - 1 > 0)
                    {
                        _scoreBuckets[i - 1].RwLock.EnterReadLock();
                    }
                }
                finally
                {
                    bucket.RwLock.ExitReadLock();
                }
            }

            return Task.FromResult(result);
        }

        public void Dispose()
        {
        }
    }
}
