using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Leaderboard.Api.Controllers.Customer.Dtos
{
    public class UpdateScoreDto
    {
        /// <summary>
        /// arbitrary positive int64 number
        /// </summary>
        [FromRoute(Name = "customerid")]
        [Required]
        public long CustomerId { get; set; }

        /// <summary>
        /// A decimal number in range of [-1000, +1000]. 
        /// </summary>
        [FromRoute(Name = "score")]
        [Range(-1000, 1000, ErrorMessage = "Score must be between -1000 and 1000.")]
        public int Score { get; set; }
    }
}