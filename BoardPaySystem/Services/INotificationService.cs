using BoardPaySystem.Models;

namespace BoardPaySystem.Services
{
    public interface INotificationService
    {
        Task CreateNotificationAsync(string userId, string title, string message, NotificationType type, string? actionLink = null, int? billId = null);
        Task CreateBillNotificationAsync(Bill bill, NotificationType type);
        Task<List<Notification>> GetUserNotificationsAsync(string userId, bool includeRead = false);
        Task MarkAsReadAsync(int notificationId);
        Task MarkAllAsReadAsync(string userId);
        Task DeleteNotificationAsync(int notificationId);
        Task<int> GetUnreadCountAsync(string userId);
        Task CheckAndCreateDueDateNotificationsAsync();
    }
}