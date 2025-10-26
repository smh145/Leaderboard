using Leaderboard.Api.Controllers.Leaderboard.Dtos;

namespace Leaderboard.Api.Interfaces
{
    public interface ILeaderboardService
    {
        Task<long> UpdateScoreAsync(long customerId, long score);
        Task<List<CustomerRankInfo>> GetRanksAsync(int start, int end);
        Task<List<CustomerRankInfo>> GetRanksByIdAsync(long customerId, int high, int low);
    }
}
