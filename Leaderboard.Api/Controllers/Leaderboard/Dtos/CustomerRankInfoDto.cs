namespace Leaderboard.Api.Controllers.Leaderboard.Dtos
{
    public class CustomerRankInfoDto
    {
        /// <summary>
        /// Customer unique Identifier
        /// </summary>
        public long CustomerId { get; set; }

        /// <summary>
        /// Customer score
        /// </summary>
        public long Score { get; set; }

        /// <summary>
        /// Customer rank
        /// </summary>
        public int Rank { get; set; }
    }
}