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

#pragma warning disable CS0067
        public event Action<string>? OnRequestStatusChanged; // PENDING, APPROVED, REJECTED, EXPIRED
        public event Action? OnSessionRevoked;
#pragma warning restore CS0067

        public SecuritySyncService(CloudSyncService cloudSyncService)
        {
            _cloudSyncService = cloudSyncService;
            _ = _cloudSyncService;
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
            var requestId = Guid.NewGuid().ToString("N");
            var req = new SecurityRequest
            {
                RequestId = requestId,
                Uid = userId,
                Type = type,
                Status = "PENDING",
                NewValue = newValue,
                CreatedAt = DateTime.UtcNow.ToString("o"),
                ExpiresAt = DateTime.UtcNow.AddHours(24).ToString("o")
            };

            if (_cloudSyncService.IsConnected)
            {
                await _cloudSyncService.UpsertPayloadAsync("security_requests", req);
            }

            return requestId;
        }

        public async Task UpdateUserSecurityStateAsync(string userId)
        {
            var state = new UserSecurityState
            {
                LastPasswordChange = DateTime.UtcNow.ToString("o"),
                ActiveSessionsRevokedAt = DateTime.UtcNow.ToString("o")
            };

            if (_cloudSyncService.IsConnected)
            {
                await _cloudSyncService.UpsertPayloadAsync("user_security_states", new
                {
                    user_id = userId,
                    last_password_change = state.LastPasswordChange,
                    active_sessions_revoked_at = state.ActiveSessionsRevokedAt
                });
            }
        }

        public void StopListeners()
        {
        }
    }
}
