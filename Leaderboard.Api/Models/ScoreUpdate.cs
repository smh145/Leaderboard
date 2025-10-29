namespace Leaderboard.Api.Controllers.Leaderboard.Dtos
{
    struct ScoreUpdate
    {
        public long CustomerId { get; }
        public long ScoreDelta { get; }

        public ScoreUpdate(long customerId, long scoreDelta)
        {
            CustomerId = customerId;
            ScoreDelta = scoreDelta;
        }
    }
}