using Leaderboard.Api.Controllers.Leaderboard.Dtos;
using Leaderboard.Api.Interfaces;
using System.Collections.Concurrent;

namespace Leaderboard.Api.Services
{
    public class LeaderboardService : ILeaderboardService
    {
        private readonly ConcurrentDictionary<long, CustomerRankInfo> _customerScores;
        private readonly SortedSet<CustomerRankInfo> _sortedScores;
        private readonly object _lock = new object();

        public LeaderboardService()
        {
            _customerScores = new ConcurrentDictionary<long, CustomerRankInfo>();
            _sortedScores = new SortedSet<CustomerRankInfo>();
        }
        public async Task<long> UpdateScoreAsync(long customerId, long score)
        {
            lock (_lock)
            {
                if (_customerScores.TryGetValue(customerId, out var oldRankInfo))
                {
                    _sortedScores.Remove(oldRankInfo);

                    oldRankInfo.Score += score;
                    _sortedScores.Add(oldRankInfo);

                    return oldRankInfo.Score;
                }
                else
                {
                    var newRankInfo = new CustomerRankInfo { CustomerId = customerId, Score = score };
                    _customerScores[customerId] = newRankInfo;
                    _sortedScores.Add(newRankInfo);
                    return newRankInfo.Score;
                }
            }
        }

        public async Task<List<CustomerRankInfo>> GetRanksAsync(int start, int end)
        {
            if (start <= 0 || end < start)
            {
                return new List<CustomerRankInfo>();
            }

            lock (_lock)
            {
                var result = _sortedScores
                    .Skip(start - 1)
                    .Take(end - start + 1)
                    .Select((info, index) => new CustomerRankInfo
                    {
                        CustomerId = info.CustomerId,
                        Score = info.Score,
                        Rank = start + index
                    })
                    .ToList();

                return result;
            }
        }

        public async Task<List<CustomerRankInfo>> GetRanksByIdAsync(long customerId, int high, int low)
        {
            lock (_lock)
            {
                if (!_customerScores.TryGetValue(customerId, out var customerRankInfo))
                {
                    return new List<CustomerRankInfo>();
                }

                var rankedList = _sortedScores.ToList();
                var customerIndex = rankedList.IndexOf(customerRankInfo);

                if (customerIndex == -1)
                {
                    return new List<CustomerRankInfo>();
                }

                var startIndex = Math.Max(0, customerIndex - high);
                var endIndex = Math.Min(rankedList.Count - 1, customerIndex + low);

                var result = new List<CustomerRankInfo>();
                for (int i = startIndex; i <= endIndex; i++)
                {
                    var info = rankedList[i];
                    result.Add(new CustomerRankInfo
                    {
                        CustomerId = info.CustomerId,
                        Score = info.Score,
                        Rank = i + 1,
                    });
                }
                return result;
            }
        }
    }
}
