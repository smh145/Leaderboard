namespace Leaderboard.Api.Controllers.Leaderboard.Dtos
{
    public class CustomerRankInfoDto
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
    }
}