using Leaderboard.Api.Controllers.Leaderboard.Dtos;
using Leaderboard.Api.Controllers.Leaderboard.Mapper;
using Leaderboard.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Leaderboard.Api.Controllers.Leaderboard
{
    [ApiController]
    [Route("[controller]")]
    public class LeaderboardController : ControllerBase
    {
        private readonly ILeaderboardService _leaderboardService;

        public LeaderboardController(ILeaderboardService leaderboardService)
        {
            _leaderboardService = leaderboardService;
        }

        /// <summary>
        /// Get customers by rank
        /// </summary>
        /// <param name="queryDto"></param>
        /// <returns>A JSON structure represents the found customers with rank and score.</returns>
        [HttpGet]
        public async Task<ActionResult<List<CustomerRankInfoDto>>> GetCustomerRanksAsync(GetCustomerRanksDto queryDto)
        {
            var list = (await _leaderboardService.GetRanksAsync(queryDto.Start, queryDto.End)).ToDto();
            return Ok(list);
        }

        /// <summary>
        /// Get customers by customerid
        /// </summary>
        /// <param name="queryDto"></param>
        /// <returns>A JSON structure represents the found customer and its nearest neighborhoods.</returns>
        [HttpGet("{customerid}")]
        public async Task<ActionResult<List<CustomerRankInfoDto>>> GetCustomerRanksByIdAsync(GetCustomerRanksByIdDto queryDto)
        {
            var list = (await _leaderboardService.GetRanksByIdAsync(queryDto.CustomerId, queryDto.High, queryDto.Low)).ToDto();
            return Ok(list);
        }
    }
}
