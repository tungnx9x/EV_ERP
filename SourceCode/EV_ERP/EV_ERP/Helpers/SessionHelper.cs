using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace EV_ERP.Helpers
{
    /// <summary>
    /// Extension methods cho Session — lưu/đọc object phức tạp
    /// </summary>
    public static class SessionExtensions
    {
        public static void SetObject<T>(this ISession session, string key, T value)
        {
            session.SetString(key, JsonConvert.SerializeObject(value));
        }

        public static T? GetObject<T>(this ISession session, string key)
        {
            var json = session.GetString(key);
            return json == null ? default : JsonConvert.DeserializeObject<T>(json);
        }
    }

    /// <summary>
    /// Thông tin user đang đăng nhập — lưu trong Session
    /// </summary>
    public class CurrentUser
    {
        public int UserId { get; set; }
        public string UserCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string RoleCode { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public int RoleId { get; set; }
        public string? AvatarUrl { get; set; }
    }

    /// <summary>
    /// Các key dùng trong Session
    /// </summary>
    public static class SessionKeys
    {
        public const string CurrentUser = "CurrentUser";
        public const string Permissions = "Permissions";
    }
}
