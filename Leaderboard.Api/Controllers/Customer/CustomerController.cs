using Leaderboard.Api.Controllers.Customer.Dtos;
using Leaderboard.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Numerics;

namespace Leaderboard.Api.Controllers.Customer
{
    [ApiController]
    [Route("[controller]")]
    public class CustomerController : ControllerBase
    {
        private readonly ICustomerService _customerService;

        public CustomerController(ICustomerService customerService)
        {
            _customerService = customerService;
        }

        /// <summary>
        /// Update Score
        /// </summary>
        /// <param name="updateDto"></param>
        /// <returns>Current score after update</returns>
        [HttpPost("{customerid}/score/{score}")]
        public async Task<ActionResult<long>> UpdateScoreAsync(UpdateScoreDto updateDto)
        {
            var score = await _customerService.UpdateScoreAsync(updateDto.CustomerId, updateDto.Score);
            return Ok(score);
        }
    }
}
