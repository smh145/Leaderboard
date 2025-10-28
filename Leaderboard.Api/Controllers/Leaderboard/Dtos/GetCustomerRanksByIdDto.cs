using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Leaderboard.Api.Controllers.Leaderboard.Dtos
{
    public class GetCustomerRanksByIdDto
    {
        /// <summary>
        /// Customer to lookup in leaderboard
        /// </summary>
        [FromRoute(Name = "customerid")]
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "CustomerId must be positive number.")]
        public long CustomerId { get; set; }

        /// <summary>
        /// Number of neighbors whose rank is higher than the specified customer.
        /// </summary>
        [FromQuery(Name = "high")]
        [Range(0, int.MaxValue, ErrorMessage = "High must be greater than or equal to 0.")]
        public int High { get; set; }

        /// <summary>
        /// Number of neighbors whose rank is lower than the specified customer.
        /// </summary>
        [FromQuery(Name = "low")]
        [Range(0, int.MaxValue, ErrorMessage = "Low must be greater than or equal to 0.")]
        public int Low { get; set; }
    }
}