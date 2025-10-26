using Leaderboard.Api.Interfaces;

namespace Leaderboard.Api.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly ILeaderboardService _leaderboardService;

        public CustomerService(ILeaderboardService leaderboardService)
        {
            _leaderboardService = leaderboardService;
        }

        public async Task<long> UpdateScoreAsync(long customerId, long score)
        {
            var newScore = await _leaderboardService.UpdateScoreAsync(customerId, score);
            return newScore;
        }
    }
}
