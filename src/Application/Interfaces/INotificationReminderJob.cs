namespace Application.Interfaces;

public interface INotificationReminderJob
{
    Task CreateRenewalRemindersAsync();
}
