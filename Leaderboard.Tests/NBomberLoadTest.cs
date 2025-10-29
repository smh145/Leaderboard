using NBomber.CSharp;
using Leaderboard.Api.Services;

namespace Leaderboard.Tests
{
    public class NBomberLoadTest
    {
        [Fact]
        public static async Task RunLoadTest()
        {
            var service = new LeaderboardService();
            var random = new Random();

            for (long i = 1; i <= 100000; i++)
            {
                await service.UpdateScoreAsync(i, random.Next(0, 1000));
            }

            int customerCount = 10000000;

            int qps = 100000;
            int updateQps = qps / 10;

            var updateScoreScenario = Scenario.Create("update_score", async context =>
            {
                var customerId = random.Next(1, customerCount);
                await service.UpdateScoreAsync(customerId, random.Next(1, 1000));
                return Response.Ok();
            })
            .WithLoadSimulations(
                Simulation.Inject(rate: updateQps, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
            );

            int getRanksQps = qps / 10 * 4;
            var getRanksScenario = Scenario.Create("get_ranks", async context =>
            {
                int start = random.Next(1, customerCount);
                int end = start + random.Next(1, 1000);
                await service.GetRanksAsync(start, end);
                return Response.Ok();
            })
            .WithLoadSimulations(
                Simulation.Inject(rate: getRanksQps, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
            );

            int getRanksByIdQps = qps / 10 * 5;
            var getRanksByIdScenario = Scenario.Create("get_ranks_by_id", async context =>
            {
                int customerId = random.Next(1, 100000);
                int high = random.Next(1, 1000);
                int low = random.Next(1, 1000);
                await service.GetRanksByIdAsync(customerId, high, low);
                return Response.Ok();
            })
            .WithLoadSimulations(
                Simulation.Inject(rate: getRanksByIdQps, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
            );

            NBomberRunner
                .RegisterScenarios(updateScoreScenario, getRanksScenario, getRanksByIdScenario)
                .Run();
        }
    }
}