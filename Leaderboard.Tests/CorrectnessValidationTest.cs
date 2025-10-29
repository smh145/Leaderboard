using Leaderboard.Api.Interfaces;
using Leaderboard.Api.Services;
using Xunit.Abstractions;

namespace Leaderboard.Tests
{
    public class CorrectnessValidationTest
    {
        private readonly ITestOutputHelper _output;

        public CorrectnessValidationTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task ValidateLeaderboardCorrectness()
        {
            ILeaderboardService service = new SnapshotLeaderboardService();
            _output.WriteLine("=== Leaderboard Correctness Validation Started ===\n");

            // Test 1: Validate score accumulation
            _output.WriteLine("Test 1: Validate Score Accumulation");
            await service.UpdateScoreAsync(1, 100);
            var score = await service.UpdateScoreAsync(1, 50);
            Assert.Equal(150, score);
            _output.WriteLine($"Y  Customer 1: Score correctly accumulated (100 + 50 = {score})");

            // Test 2: Validate ranking order (higher score = lower rank number)
            _output.WriteLine("\nTest 2: Validate Ranking Order");
            await service.UpdateScoreAsync(2, 200);
            await service.UpdateScoreAsync(3, 150);
            await service.UpdateScoreAsync(4, 100);
            await service.UpdateScoreAsync(5, 50);

            await Task.Delay(200);

            var top5 = await service.GetRanksAsync(1, 5);
            Assert.Equal(5, top5.Count);
            Assert.Equal(2, top5[0].CustomerId);
            Assert.Equal(200, top5[0].Score);
            Assert.Equal(1, top5[0].Rank);
            
            _output.WriteLine($"Y  Rank 1: Customer {top5[0].CustomerId}, Score {top5[0].Score}");
            _output.WriteLine($"Y  Rank 2: Customer {top5[1].CustomerId}, Score {top5[1].Score}");
            _output.WriteLine($"Y  Rank 3: Customer {top5[2].CustomerId}, Score {top5[2].Score}");
            _output.WriteLine($"Y  Rank 4: Customer {top5[3].CustomerId}, Score {top5[3].Score}");
            _output.WriteLine($"Y  Rank 5: Customer {top5[4].CustomerId}, Score {top5[4].Score}");

            // Test 3: Validate tie-breaking (same score, lower customer ID ranks higher)
            _output.WriteLine("\nTest 3: Validate Tie-Breaking Rules");
            Assert.True(top5[1].Score == top5[2].Score, "Customers 1 and 3 should have the same score");
            Assert.True(top5[1].CustomerId < top5[2].CustomerId, "Lower customer ID should rank higher when scores are equal");
            _output.WriteLine($"Y  Tie-breaking verified: Customer {top5[1].CustomerId} ranks before Customer {top5[2].CustomerId} (both have score 150)");

            // Test 4: Validate GetRanksByIdAsync with context window
            _output.WriteLine("\nTest 4: Validate GetRanksByIdAsync");
            var contextResults = await service.GetRanksByIdAsync(3, 1, 2);
            Assert.True(contextResults.Count >= 2 && contextResults.Count <= 4, "Should return customer 3 with context");
            var customer3 = contextResults.FirstOrDefault(r => r.CustomerId == 3);
            Assert.NotNull(customer3);
            Assert.Equal(3, customer3.Rank);
            _output.WriteLine($"Y  Customer 3 context window: Rank {customer3.Rank}, with {contextResults.Count} total results");

            // Test 5: Validate ranking updates after score changes
            _output.WriteLine("\nTest 5: Validate Ranking Updates After Score Changes");
            await service.UpdateScoreAsync(5, 200);

            await Task.Delay(200);

            var updatedTop3 = await service.GetRanksAsync(1, 3);
            Assert.Equal(5, updatedTop3[0].CustomerId);
            Assert.Equal(250, updatedTop3[0].Score);
            Assert.Equal(1, updatedTop3[0].Rank);
            _output.WriteLine($"Y  Customer 5 moved to Rank 1 after update (new score: {updatedTop3[0].Score})");

            // Test 6: Validate boundary conditions
            _output.WriteLine("\nTest 6: Validate Boundary Conditions");
            
            var emptyResult = await service.GetRanksAsync(100, 200);
            Assert.Empty(emptyResult);
            _output.WriteLine("Y  Empty result for out-of-range query");

            var invalidRange = await service.GetRanksAsync(5, 1);
            Assert.Empty(invalidRange);
            _output.WriteLine("Y  Empty result for invalid range (end < start)");

            var nonExistent = await service.GetRanksByIdAsync(999, 1, 1);
            Assert.Empty(nonExistent);
            _output.WriteLine("Y  Empty result for non-existent customer");

            // Test 7: Validate large-scale consistency
            _output.WriteLine("\nTest 7: Validate Large-Scale Consistency");
            ILeaderboardService service2 = new SnapshotLeaderboardService();
            var random = new Random(42);
            var expectedScores = new Dictionary<long, long>();

            for (long i = 1; i <= 100; i++)
            {
                var scoreIncrement = random.Next(1, 1000);
                var finalScore = await service2.UpdateScoreAsync(i, scoreIncrement);
                expectedScores[i] = finalScore;
            }

            await Task.Delay(200);

            var allRanks = await service2.GetRanksAsync(1, 100);
            Assert.Equal(100, allRanks.Count);
            _output.WriteLine($"Y  All 100 customers are ranked");

            for (int i = 0; i < allRanks.Count - 1; i++)
            {
                var current = allRanks[i];
                var next = allRanks[i + 1];
                
                Assert.True(current.Score > next.Score || 
                           (current.Score == next.Score && current.CustomerId < next.CustomerId),
                           $"Ranking order violated at position {i}");
                
                Assert.Equal(expectedScores[current.CustomerId], current.Score);
            }
            _output.WriteLine($"Y  Ranking order is correct (descending by score, ascending by customer ID for ties)");
            _output.WriteLine($"Y  All scores match expected values");

            // Test 8: Validate rank numbering is continuous
            _output.WriteLine("\nTest 8: Validate Rank Numbering");
            for (int i = 0; i < allRanks.Count; i++)
            {
                Assert.Equal(i + 1, allRanks[i].Rank);
            }
            _output.WriteLine($"Y  Rank numbering is continuous from 1 to {allRanks.Count}");

            // Test 9: Validate cross-bucket ranking
            _output.WriteLine("\nTest 9: Validate Cross-Bucket Ranking");
            ILeaderboardService service3 = new SnapshotLeaderboardService();
            await service3.UpdateScoreAsync(1, 50);
            await service3.UpdateScoreAsync(2, 150);
            await service3.UpdateScoreAsync(3, 250);
            await service3.UpdateScoreAsync(4, 350);

            await Task.Delay(200);

            var crossBucket = await service3.GetRanksAsync(1, 4);
            Assert.Equal(4, crossBucket.Count);
            Assert.Equal(350, crossBucket[0].Score);
            Assert.Equal(50, crossBucket[3].Score);
            _output.WriteLine($"Y  Cross-bucket ranking works correctly");

            _output.WriteLine("\n=== All Correctness Validations Passed Y  ===");
        }

        [Fact]
        public async Task ValidateConcurrentOperationsCorrectness()
        {
            _output.WriteLine("=== Concurrent Operations Validation Started ===\n");

            ILeaderboardService service = new SnapshotLeaderboardService();
            var random = new Random();

            for (long i = 1; i <= 50; i++)
            {
                await service.UpdateScoreAsync(i, random.Next(1, 500));
            }

            _output.WriteLine("Test: Concurrent Reads and Writes");
            var tasks = new List<Task>();

            for (int i = 0; i < 10; i++)
            {
                var customerId = random.Next(1, 50);
                tasks.Add(Task.Run(async () => await service.UpdateScoreAsync(customerId, random.Next(1, 100))));
            }

            await Task.Delay(200);

            for (int i = 0; i < 20; i++)
            {
                tasks.Add(Task.Run(async () => await service.GetRanksAsync(1, 10)));
            }

            for (int i = 0; i < 10; i++)
            {
                var customerId = random.Next(1, 50);
                tasks.Add(Task.Run(async () => await service.GetRanksByIdAsync(customerId, 5, 5)));
            }

            await Task.WhenAll(tasks);
            _output.WriteLine($"Y  Completed {tasks.Count} concurrent operations without errors");

            var finalRanks = await service.GetRanksAsync(1, 50);
            Assert.True(finalRanks.Count > 0, "Should have rankings after concurrent operations");
            
            for (int i = 0; i < finalRanks.Count - 1; i++)
            {
                var current = finalRanks[i];
                var next = finalRanks[i + 1];
                Assert.True(current.Score > next.Score || 
                           (current.Score == next.Score && current.CustomerId < next.CustomerId),
                           "Ranking order should be maintained after concurrent operations");
            }
            _output.WriteLine($"Y  Data consistency maintained after concurrent operations");
            _output.WriteLine($"Y  Ranking order is still correct");

            _output.WriteLine("\n=== Concurrent Operations Validation Passed Y  ===");
        }
    }
}