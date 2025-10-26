namespace Leaderboard.Api.Controllers.Leaderboard.Dtos
{
    public class CustomerRankInfo : IComparable<CustomerRankInfo>
    {
        /// <summary>
        /// Customer Unique Identifier
        /// </summary>
        public long CustomerId { get; set; }

        /// <summary>
        /// Customer Score
        /// </summary>
        public long Score { get; set; }

        /// <summary>
        /// Customer Rank
        /// </summary>
        public int Rank { get; set; }

        public int CompareTo(CustomerRankInfo? other)
        {
            if (other == null) return 1;

            int scoreComp = other.Score.CompareTo(this.Score);
            if (scoreComp != 0)
            {
                return scoreComp;
            }

            return this.CustomerId.CompareTo(other.CustomerId);
        }
    }
}