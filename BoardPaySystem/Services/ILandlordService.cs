namespace BoardPaySystem.Services
{
    public interface ILandlordService
    {
        Task<Dictionary<string, object>> GetDashboardStatsAsync();
    }
}