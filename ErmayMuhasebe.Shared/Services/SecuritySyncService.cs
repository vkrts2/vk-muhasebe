using System;
using System.Threading.Tasks;

namespace ErmayMuhasebe.Services
{
    public class SecurityRequest
    {
        public string RequestId { get; set; } = "";
        public string Uid { get; set; } = "";
        public string Type { get; set; } = ""; // PASSWORD_CHANGE, USERNAME_CHANGE
        public string Status { get; set; } = "PENDING"; // PENDING, APPROVED, REJECTED, EXPIRED
        public string NewValue { get; set; } = ""; // Yeni şifre (hashlenmiş) veya yeni kullanıcı adı
        public string CreatedAt { get; set; } = "";
        public string ExpiresAt { get; set; } = "";
    }

    public class UserSecurityState
    {
        public string LastPasswordChange { get; set; } = "";
        public string ActiveSessionsRevokedAt { get; set; } = "";
    }

    public class SecuritySyncService
    {
        private readonly CloudSyncService _cloudSyncService;

        public event Action<string>? OnRequestStatusChanged; // PENDING, APPROVED, REJECTED, EXPIRED
        public event Action? OnSessionRevoked;

        public SecuritySyncService(CloudSyncService cloudSyncService)
        {
            _cloudSyncService = cloudSyncService;
        }

        public void ListenToSecurityRequest(string requestId)
        {
            // Supabase security listener stub
        }

        public void ListenToSessionStatus(string userId, DateTime sessionStartTime)
        {
            // Supabase session status listener stub
        }

        public async Task<string> CreateSecurityRequestAsync(string userId, string type, string newValue)
        {
            await Task.CompletedTask;
            return Guid.NewGuid().ToString("N");
        }

        public async Task UpdateUserSecurityStateAsync(string userId)
        {
            await Task.CompletedTask;
        }

        public void StopListeners()
        {
        }
    }
}
