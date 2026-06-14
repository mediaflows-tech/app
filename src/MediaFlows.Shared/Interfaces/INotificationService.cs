using MediaFlows.Shared.DTOs;
using MediaFlows.Shared.Models.Entities;
using MediaFlows.Shared.Models.Enums;

namespace MediaFlows.Shared.Interfaces;

public interface INotificationService
{
    Task CreateNotificationAsync(string userId, string title, string message, NotificationType type);
    Task<List<Notification>> GetUserNotificationsAsync(string userId, int limit = 20);
    Task<int> GetUnreadCountAsync(string userId);
    Task MarkReadAsync(int notificationId);
    Task MarkAllReadAsync(string userId);
}
