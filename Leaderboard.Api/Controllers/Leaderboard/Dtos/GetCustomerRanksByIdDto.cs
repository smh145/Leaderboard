using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Leaderboard.Api.Controllers.Leaderboard.Dtos
{
    public class GetCustomerRanksByIdDto
    {
        /// <summary>
        /// customer to lookup in leaderboard
        /// </summary>
        [FromRoute(Name = "customerid")]
        [Required]
        public long CustomerId { get; set; }

        /// <summary>
        /// number of neighbors whose rank is higher than the specified customer.
        /// </summary>
        [FromQuery(Name = "high")]
        [Range(0, int.MaxValue, ErrorMessage = "High must be greater than or equal to 0.")]
        public int High { get; set; }

        /// <summary>
        /// number of neighbors whose rank is lower than the specified customer.
        /// </summary>
        [FromQuery(Name = "low")]
        [Range(0, int.MaxValue, ErrorMessage = "Low must be greater than or equal to 0.")]
        public int Low { get; set; }
    }
}