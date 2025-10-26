namespace Leaderboard.Api.Interfaces
{
    public interface ICustomerService
    {
        Task<long> UpdateScoreAsync(long customerId, long score);
    }
}
