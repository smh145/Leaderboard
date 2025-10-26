using Leaderboard.Api.Interfaces;
using Leaderboard.Api.Services;

namespace Leaderboard.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddSingleton<ILeaderboardService, LeaderboardService>();
            builder.Services.AddScoped<ICustomerService, CustomerService>();

            builder.Services.AddControllers();

            var app = builder.Build();

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
