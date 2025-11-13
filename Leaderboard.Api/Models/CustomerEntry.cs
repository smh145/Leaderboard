namespace Leaderboard.Api.Controllers.Models
{
    struct CustomerEntry : IComparable<CustomerEntry>
    {
        public long Score { get; }
        public long CustomerId { get; }

        public CustomerEntry(long score, long customerId)
        {
            Score = score;
            CustomerId = customerId;
        }

        public int CompareTo(CustomerEntry other)
        {
            int scoreCompare = other.Score.CompareTo(Score);
            if (scoreCompare != 0) return scoreCompare;
            return CustomerId.CompareTo(other.CustomerId);
        }
    }
}