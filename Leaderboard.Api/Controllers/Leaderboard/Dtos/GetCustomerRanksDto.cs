using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Leaderboard.Api.Controllers.Leaderboard.Dtos
{
    public class GetCustomerRanksDto : IValidatableObject
    {
        /// <summary>
        /// Start rank, included in response if exists
        /// </summary>
        [FromQuery(Name = "start")]
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Start must be positive number.")]
        public int Start { get; set; }

        /// <summary>
        /// End rank, included in response if exists
        /// </summary>
        [FromQuery(Name = "end")]
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "End must be positive number.")]
        public int End { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Start > End)
            {
                yield return new ValidationResult("Start must be less than or equal to End.", new[] { nameof(Start), nameof(End) });
            }
        }
    }
}