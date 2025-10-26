using Leaderboard.Api.Controllers.Leaderboard.Dtos;

namespace Leaderboard.Api.Controllers.Leaderboard.Mapper
{
    // Maybe use AutoMapper
    public static class CustomerRankInfoMapper
    {
        public static CustomerRankInfoDto ToDto(this CustomerRankInfo customerRankInfo)
        {
            return new CustomerRankInfoDto
            {
                CustomerId = customerRankInfo.CustomerId,
                Score = customerRankInfo.Score,
                Rank = customerRankInfo.Rank
            };
        }

        public static List<CustomerRankInfoDto> ToDto(this IEnumerable<CustomerRankInfo> customerRankInfos)
        {
            return customerRankInfos.Select(c => c.ToDto()).ToList();
        }
    }
}