using Leaderboard.Api.Interfaces;
using Leaderboard.Api.Services;
using NBomber.CSharp;
using System.Collections.Concurrent;

namespace Leaderboard.Tests
{
    public class NBomberLoadTest
    {
        [Fact]
        public static async Task RunLoadTest()
        {
            ILeaderboardService service = new SnapshotLeaderboardService();
            var random = new Random();

            var hash = new ConcurrentDictionary<long, byte>();
            var customerIds = new ConcurrentBag<long>();

            for (long i = 1; i <= 100000; i++)
            {
                await service.UpdateScoreAsync(i, random.Next(0, 1000));
                hash.TryAdd(i, 0);
                customerIds.Add(i);
            }

            int customerCount = 10000000;

            int seconds = 5;
            int qps = 100000;
            int interval = 100;
            int updateQps = qps / 10 / (1000 / interval);

            var updateScoreScenario = Scenario.Create("update_score", async context =>
            {
                var customerId = random.Next(1, customerCount);
                if (!hash.ContainsKey(customerId))
                {
                    if (hash.TryAdd(customerId, 0))
                    {
                        customerIds.Add(customerId);
                    }
                }

                await service.UpdateScoreAsync(customerId, random.Next(1, 1000));
                return Response.Ok();
            })
            .WithLoadSimulations(
                Simulation.Inject(rate: updateQps, interval: TimeSpan.FromMilliseconds(interval), during: TimeSpan.FromSeconds(seconds))
            );

            int getRanksQps = qps / 10 * 4 / (1000 / interval);
            var getRanksScenario = Scenario.Create("get_ranks", async context =>
            {
                int count = customerIds.Count;
                if (count == 0) return Response.Ok();

                int start = random.Next(1, Math.Max(2, count));
                int end = start + random.Next(1, 1000);
                await service.GetRanksAsync(start, end);
                return Response.Ok();
            })
            .WithLoadSimulations(
                Simulation.Inject(rate: getRanksQps, interval: TimeSpan.FromMilliseconds(interval), during: TimeSpan.FromSeconds(seconds))
            );

            int getRanksByIdQps = qps / 10 * 5 / (1000 / interval);
            var getRanksByIdScenario = Scenario.Create("get_ranks_by_id", async context =>
            {
                var ids = customerIds.ToArray();
                if (ids.Length == 0) return Response.Ok();

                int index = random.Next(0, ids.Length);
                long customerId = ids[index];
                int high = random.Next(1, 1000);
                int low = random.Next(1, 1000);
                await service.GetRanksByIdAsync(customerId, high, low);
                return Response.Ok();
            })
            .WithLoadSimulations(
                Simulation.Inject(rate: getRanksByIdQps, interval: TimeSpan.FromMilliseconds(interval), during: TimeSpan.FromSeconds(seconds))
            );

            NBomberRunner
                .RegisterScenarios(updateScoreScenario, getRanksScenario, getRanksByIdScenario)
                .Run();
        }
    }
}