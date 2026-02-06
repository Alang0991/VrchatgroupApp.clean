using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VrchatgroupApp.clean.Services
{
    [Flags]
    public enum GroupPermission
    {
        None = 0,
        ManageGroup = 1 << 0,
        ManageMembers = 1 << 1,
        Moderate = 1 << 2,
        ManageRoles = 1 << 3
    }

    public interface IVRChatApiService
    {
        bool IsLoggedIn { get; }

        string? CurrentUserId { get; }
        string? CurrentUserDisplayName { get; }
        string? CurrentUserProfilePicUrl { get; }

        Task<LoginResult> LoginAsync(string username, string password);
        Task<LoginResult> Verify2FAAsync(string code, string authType);
        Task<LoginResult> RestoreSessionAsync(string authCookie, string? twoFactorCookie);

        string? GetAuthCookie();
        string? GetTwoFactorCookie();
        void Logout();

        // ✅ Dashboard: fast + only groups you can moderate/own/staff in
        Task<List<GroupInfo>> GetMyManageableGroupsAsync(int take = 5);

        // Basic group actions
        Task JoinGroupAsync(string groupId);
        Task LeaveGroupAsync(string groupId);

        // Group details
        Task<GroupInfo> GetGroupAsync(string groupId);

        // Permissions (used when opening detail pages, NOT at startup)
        Task<GroupPermission> GetMyPermissionsForGroupAsync(string groupId);

        // Members
        Task<List<GroupMemberInfo>> GetGroupMembersAsync(string groupId, int n = 50, int offset = 0);
        Task<GroupMemberInfo?> GetGroupMemberAsync(string groupId, string userId);
        Task KickGroupMemberAsync(string groupId, string userId);

        // Bans
        Task<List<GroupBanInfo>> GetGroupBansAsync(string groupId, int n = 50, int offset = 0);
        Task BanGroupMemberAsync(string groupId, string userId);
        Task UnbanGroupMemberAsync(string groupId, string userId);

        // Roles
        Task<List<GroupRoleInfo>> GetGroupRolesAsync(string groupId);
        Task AddRoleToMemberAsync(string groupId, string userId, string roleId);
        Task RemoveRoleFromMemberAsync(string groupId, string userId, string roleId);

        // Invites
        Task InviteUserToGroupAsync(string groupId, string userId);

        // Update group metadata (description, etc.)
        Task UpdateGroupAsync(string groupId, GroupUpdateRequest update);
    }

    public class VRChatApiService : IVRChatApiService
    {
        private const string BaseUrl = "https://api.vrchat.cloud/api/1/";
        private const string ApiKey = "JlE5Jldo5Jibnk5O5hTx6XVqsJu4WJ26";

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly HttpClient _client;
        private readonly CookieContainer _cookies;

        public bool IsLoggedIn { get; private set; }

        public string? CurrentUserId { get; private set; }
        public string? CurrentUserDisplayName { get; private set; }
        public string? CurrentUserProfilePicUrl { get; private set; }

        public VRChatApiService()
        {
            _cookies = new CookieContainer();

            var handler = new HttpClientHandler
            {
                CookieContainer = _cookies,
                UseCookies = true,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            _client = new HttpClient(handler)
            {
                BaseAddress = new Uri(BaseUrl),
                Timeout = TimeSpan.FromSeconds(30) // ✅ prevents “forever loading”
            };

            _client.DefaultRequestHeaders.Add("User-Agent", "VrchatgroupApp/1.0");
            _client.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        // ───────────────────────── AUTH ─────────────────────────

        public async Task<LoginResult> LoginAsync(string username, string password)
        {
            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));

            var request = new HttpRequestMessage(HttpMethod.Get, WithKey("auth/user"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

            var response = await _client.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return new LoginResult { Success = false, Message = ExtractErrorMessage(json) ?? "Login failed" };

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("requiresTwoFactorAuth", out _))
            {
                return new LoginResult
                {
                    Success = false,
                    Requires2FA = true,
                    TwoFactorTypes = new() { "totp" },
                    Message = "2FA required"
                };
            }

            SetCurrentUserFromAuthUser(root);
            IsLoggedIn = true;

            return new LoginResult { Success = true, Message = "Login successful" };
        }

        public async Task<LoginResult> Verify2FAAsync(string code, string authType)
        {
            var endpoint = authType switch
            {
                "totp" => "auth/twofactorauth/totp/verify",
                "emailOtp" => "auth/twofactorauth/emailOtp/verify",
                _ => "auth/twofactorauth/totp/verify"
            };

            var request = new HttpRequestMessage(HttpMethod.Post, WithKey(endpoint))
            {
                Content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("code", code)
                })
            };

            var response = await _client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return new LoginResult { Success = false, Message = ExtractErrorMessage(body) ?? "Invalid 2FA code" };

            var meResp = await _client.GetAsync(WithKey("auth/user"));
            var meJson = await meResp.Content.ReadAsStringAsync();

            if (!meResp.IsSuccessStatusCode)
                return new LoginResult { Success = false, Message = ExtractErrorMessage(meJson) ?? "2FA OK but failed to load profile" };

            using var doc = JsonDocument.Parse(meJson);
            SetCurrentUserFromAuthUser(doc.RootElement);

            IsLoggedIn = true;
            return new LoginResult { Success = true, Message = "2FA verified" };
        }

        public async Task<LoginResult> RestoreSessionAsync(string authCookie, string? twoFactorCookie)
        {
            var baseUri = new Uri("https://api.vrchat.cloud/");

            _cookies.Add(baseUri, new Cookie("auth", authCookie, "/", ".vrchat.cloud"));

            if (!string.IsNullOrWhiteSpace(twoFactorCookie))
                _cookies.Add(baseUri, new Cookie("twoFactorAuth", twoFactorCookie, "/", ".vrchat.cloud"));

            var resp = await _client.GetAsync(WithKey("auth/user"));
            var json = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                Logout();
                return new LoginResult { Success = false, Message = ExtractErrorMessage(json) ?? "Session expired" };
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("requiresTwoFactorAuth", out _))
            {
                Logout();
                return new LoginResult { Success = false, Requires2FA = true, Message = "Session requires 2FA again" };
            }

            SetCurrentUserFromAuthUser(root);
            IsLoggedIn = true;

            return new LoginResult { Success = true, Message = "Session restored" };
        }

        public string? GetAuthCookie() => GetCookie("auth");
        public string? GetTwoFactorCookie() => GetCookie("twoFactorAuth");

        private string? GetCookie(string name)
        {
            try
            {
                foreach (Cookie c in _cookies.GetCookies(new Uri("https://api.vrchat.cloud/")))
                    if (c.Name == name) return c.Value;
            }
            catch { }
            return null;
        }

        public void Logout()
        {
            IsLoggedIn = false;
            CurrentUserId = null;
            CurrentUserDisplayName = null;
            CurrentUserProfilePicUrl = null;
        }

        // ───────────────────────── FAST DASHBOARD GROUPS ─────────────────────────
        // ✅ NO per-group permission calls here (that’s what caused huge lag)
        // ✅ Only groups where the users/{id}/groups item says you’re owner/mod/staff
        // ✅ Limit to first 5 by default
        public async Task<List<GroupInfo>> GetMyManageableGroupsAsync(int take = 5)
        {
            var result = new List<GroupInfo>();

            if (string.IsNullOrEmpty(CurrentUserId))
                return result;

            var response = await _client.GetAsync(WithKey($"users/{CurrentUserId}/groups") + "&n=100&offset=0");
            if (!response.IsSuccessStatusCode)
                return result;

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            foreach (var g in doc.RootElement.EnumerateArray())
            {
                // Owner
                bool isOwner = g.TryGetProperty("ownerId", out var ownerId) &&
                               ownerId.GetString() == CurrentUserId;

                // Some responses include these flags
                bool isModerator = g.TryGetProperty("isModerator", out var modEl) &&
                                   modEl.ValueKind == JsonValueKind.True;

                bool isStaff = g.TryGetProperty("isStaff", out var staffEl) &&
                               staffEl.ValueKind == JsonValueKind.True;

                // If VRChat doesn’t include mod flags in your response, we keep it fast by skipping.
                // Permissions are checked later when you open the group.
                if (!(isOwner || isModerator || isStaff))
                    continue;

                result.Add(ParseGroupInfoFromUserGroupsItem(g));

                if (result.Count >= Math.Max(1, take))
                    break;
            }

            return result;
        }

        // ───────────────────────── GROUP BASIC ─────────────────────────

        public async Task JoinGroupAsync(string groupId)
        {
            var response = await _client.PostAsync(WithKey($"groups/{groupId}/join"), null);
            await EnsureOkAsync(response);
        }

        public async Task LeaveGroupAsync(string groupId)
        {
            var response = await _client.PostAsync(WithKey($"groups/{groupId}/leave"), null);
            await EnsureOkAsync(response);
        }

        public async Task<GroupInfo> GetGroupAsync(string groupId)
        {
            var response = await _client.GetAsync(WithKey($"groups/{groupId}"));
            await EnsureOkAsync(response);

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return ParseGroupInfoFromGroupObject(doc.RootElement);
        }

        // ───────────────────────── PERMISSIONS (ONLY WHEN OPENING A GROUP) ─────────────────────────

        public async Task<GroupPermission> GetMyPermissionsForGroupAsync(string groupId)
        {
            if (CurrentUserId == null)
                return GroupPermission.None;

            var memberResp = await _client.GetAsync(WithKey($"groups/{groupId}/members/{CurrentUserId}"));
            if (!memberResp.IsSuccessStatusCode)
                return GroupPermission.None;

            using var memberDoc = JsonDocument.Parse(await memberResp.Content.ReadAsStringAsync());
            var member = memberDoc.RootElement;

            if (member.TryGetProperty("isOwner", out var isOwner) && isOwner.ValueKind == JsonValueKind.True)
            {
                return GroupPermission.ManageGroup |
                       GroupPermission.ManageMembers |
                       GroupPermission.Moderate |
                       GroupPermission.ManageRoles;
            }

            if (member.TryGetProperty("isModerator", out var isMod) && isMod.ValueKind == JsonValueKind.True)
            {
                return GroupPermission.Moderate | GroupPermission.ManageMembers;
            }

            // If role-based permissions exist, you can extend here later.
            return GroupPermission.None;
        }

        // ───────────────────────── MEMBERS ─────────────────────────

        public async Task<List<GroupMemberInfo>> GetGroupMembersAsync(string groupId, int n = 50, int offset = 0)
        {
            var response = await _client.GetAsync(WithKey($"groups/{groupId}/members") + $"&n={n}&offset={offset}");
            await EnsureOkAsync(response);

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            var list = new List<GroupMemberInfo>();
            foreach (var m in doc.RootElement.EnumerateArray())
                list.Add(ParseMember(m));

            return list;
        }

        public async Task<GroupMemberInfo?> GetGroupMemberAsync(string groupId, string userId)
        {
            var response = await _client.GetAsync(WithKey($"groups/{groupId}/members/{userId}"));
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            await EnsureOkAsync(response);

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return ParseMember(doc.RootElement);
        }

        public async Task KickGroupMemberAsync(string groupId, string userId)
        {
            var response = await _client.DeleteAsync(WithKey($"groups/{groupId}/members/{userId}"));
            await EnsureOkAsync(response);
        }

        // ───────────────────────── BANS ─────────────────────────

        public async Task<List<GroupBanInfo>> GetGroupBansAsync(string groupId, int n = 50, int offset = 0)
        {
            var response = await _client.GetAsync(WithKey($"groups/{groupId}/bans") + $"&n={n}&offset={offset}");
            await EnsureOkAsync(response);

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            var list = new List<GroupBanInfo>();
            foreach (var b in doc.RootElement.EnumerateArray())
                list.Add(ParseBan(b));

            return list;
        }

        public async Task BanGroupMemberAsync(string groupId, string userId)
        {
            var payload = new Dictionary<string, string> { ["userId"] = userId };
            var response = await PostJsonAsync(WithKey($"groups/{groupId}/bans"), payload);
            await EnsureOkAsync(response);
        }

        public async Task UnbanGroupMemberAsync(string groupId, string userId)
        {
            var response = await _client.DeleteAsync(WithKey($"groups/{groupId}/bans/{userId}"));
            await EnsureOkAsync(response);
        }

        // ───────────────────────── ROLES ─────────────────────────

        public async Task<List<GroupRoleInfo>> GetGroupRolesAsync(string groupId)
        {
            var response = await _client.GetAsync(WithKey($"groups/{groupId}/roles"));
            await EnsureOkAsync(response);

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            var list = new List<GroupRoleInfo>();
            foreach (var r in doc.RootElement.EnumerateArray())
                list.Add(ParseRole(r));

            return list;
        }

        public async Task AddRoleToMemberAsync(string groupId, string userId, string roleId)
        {
            var response = await _client.PutAsync(WithKey($"groups/{groupId}/members/{userId}/roles/{roleId}"), null);
            await EnsureOkAsync(response);
        }

        public async Task RemoveRoleFromMemberAsync(string groupId, string userId, string roleId)
        {
            var response = await _client.DeleteAsync(WithKey($"groups/{groupId}/members/{userId}/roles/{roleId}"));
            await EnsureOkAsync(response);
        }

        // ───────────────────────── INVITES ─────────────────────────

        public async Task InviteUserToGroupAsync(string groupId, string userId)
        {
            var payload = new Dictionary<string, string> { ["userId"] = userId };
            var response = await PostJsonAsync(WithKey($"groups/{groupId}/invites"), payload);
            await EnsureOkAsync(response);
        }

        // ───────────────────────── UPDATE GROUP ─────────────────────────

        public async Task UpdateGroupAsync(string groupId, GroupUpdateRequest update)
        {
            var response = await PutJsonAsync(WithKey($"groups/{groupId}"), update);
            await EnsureOkAsync(response);
        }

        // ───────────────────────── HELPERS ─────────────────────────

        private static string WithKey(string path) => $"{path}?apiKey={ApiKey}";

        private static string? ExtractErrorMessage(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("error", out var err)) return err.GetString();
                if (doc.RootElement.TryGetProperty("message", out var msg)) return msg.GetString();
            }
            catch { }
            return null;
        }

        private static async Task EnsureOkAsync(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode) return;

            var body = await response.Content.ReadAsStringAsync();
            var msg = ExtractErrorMessage(body) ?? body;

            if (msg.Length > 500) msg = msg.Substring(0, 500);

            throw new HttpRequestException($"VRChat API error {(int)response.StatusCode} {response.ReasonPhrase}: {msg}");
        }

        private static StringContent ToJsonContent<T>(T payload)
        {
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            return new StringContent(json, Encoding.UTF8, "application/json");
        }

        private Task<HttpResponseMessage> PostJsonAsync<T>(string url, T payload)
            => _client.PostAsync(url, ToJsonContent(payload));

        private Task<HttpResponseMessage> PutJsonAsync<T>(string url, T payload)
            => _client.PutAsync(url, ToJsonContent(payload));

        private static GroupInfo ParseGroupInfoFromUserGroupsItem(JsonElement g)
        {
            return new GroupInfo
            {
                Id = g.TryGetProperty("groupId", out var gid) ? gid.GetString() ?? "" : "",
                Name = g.TryGetProperty("name", out var nm) ? nm.GetString() ?? "" : "",
                MemberCount = g.TryGetProperty("memberCount", out var mc) ? mc.GetInt32() : 0,
                OnlineCount = g.TryGetProperty("onlineMemberCount", out var oc) ? oc.GetInt32() : 0,
                IconUrl = g.TryGetProperty("iconUrl", out var icon) ? icon.GetString() : null,
                BannerUrl = g.TryGetProperty("bannerUrl", out var banner) ? banner.GetString() : null
            };
        }

        private static GroupInfo ParseGroupInfoFromGroupObject(JsonElement g)
        {
            return new GroupInfo
            {
                Id = g.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                Name = g.TryGetProperty("name", out var nm) ? nm.GetString() ?? "" : "",
                MemberCount = g.TryGetProperty("memberCount", out var mc) ? mc.GetInt32() : 0,
                OnlineCount = g.TryGetProperty("onlineMemberCount", out var oc) ? oc.GetInt32() : 0,
                IconUrl = g.TryGetProperty("iconUrl", out var icon) ? icon.GetString() : null,
                BannerUrl = g.TryGetProperty("bannerUrl", out var banner) ? banner.GetString() : null
            };
        }

        private static GroupMemberInfo ParseMember(JsonElement m)
        {
            return new GroupMemberInfo
            {
                UserId = m.TryGetProperty("userId", out var uid) ? uid.GetString() ?? "" : "",
                DisplayName = m.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? "" : "",
                ProfileImageUrl =
                    m.TryGetProperty("profilePicOverride", out var ppo) ? ppo.GetString() :
                    m.TryGetProperty("thumbnailUrl", out var th) ? th.GetString() : null,
                RoleIds = m.TryGetProperty("roleIds", out var roles) && roles.ValueKind == JsonValueKind.Array
                    ? ReadStringArray(roles)
                    : new List<string>()
            };
        }

        private static GroupBanInfo ParseBan(JsonElement b)
        {
            return new GroupBanInfo
            {
                UserId = b.TryGetProperty("userId", out var uid) ? uid.GetString() ?? "" : "",
                DisplayName = b.TryGetProperty("displayName", out var dn) ? dn.GetString() : null,
                CreatedAt = b.TryGetProperty("createdAt", out var ca) ? ca.GetString() : null
            };
        }

        private static GroupRoleInfo ParseRole(JsonElement r)
        {
            return new GroupRoleInfo
            {
                RoleId = r.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                Name = r.TryGetProperty("name", out var nm) ? nm.GetString() ?? "" : "",
                Description = r.TryGetProperty("description", out var d) ? d.GetString() : null
            };
        }

        private static List<string> ReadStringArray(JsonElement arr)
        {
            var list = new List<string>();
            foreach (var e in arr.EnumerateArray())
            {
                if (e.ValueKind == JsonValueKind.String)
                    list.Add(e.GetString() ?? "");
            }
            return list;
        }

        private void SetCurrentUserFromAuthUser(JsonElement root)
        {
            CurrentUserId = root.TryGetProperty("id", out var id) ? id.GetString() : null;
            CurrentUserDisplayName = root.TryGetProperty("displayName", out var dn) ? dn.GetString() : null;

            if (root.TryGetProperty("userIcon", out var icon))
                CurrentUserProfilePicUrl = icon.GetString();

            if (string.IsNullOrWhiteSpace(CurrentUserProfilePicUrl) &&
                root.TryGetProperty("currentAvatarThumbnailImageUrl", out var thumb))
                CurrentUserProfilePicUrl = thumb.GetString();
        }
    }

    // ───────────────────────── MODELS (kept in Services namespace for your ViewModels) ─────────────────────────

    public class GroupMemberInfo
    {
        public string UserId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string? ProfileImageUrl { get; set; }
        public List<string> RoleIds { get; set; } = new();
    }

    public class GroupRoleInfo
    {
        public string RoleId { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Description { get; set; }
    }

    public class GroupBanInfo
    {
        public string UserId { get; set; } = "";
        public string? DisplayName { get; set; }
        public string? CreatedAt { get; set; }
    }

    public class GroupUpdateRequest
    {
        public string? Description { get; set; }
    }
}
