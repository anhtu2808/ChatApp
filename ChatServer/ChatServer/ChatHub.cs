using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace ChatServer
{
    public class ChatHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            UserManager.SetOfflineByConnection(Context.ConnectionId);
            await Clients.All.SendAsync("UpdateUserList", UserManager.GetAll());
            await base.OnDisconnectedAsync(exception);
        }

        public async Task Register(string username)
        {
            // Optional: bạn có thể normalize username về viết thường nếu cần
            UserManager.SetOnline(username.Trim(), Context.ConnectionId);
            await Clients.All.SendAsync("UpdateUserList", UserManager.GetAll());
        }

        public async Task SendMessage(string user, string message)
        {
            await Clients.All.SendAsync("ReceiveMessage", user, message);
        }

        public async Task Typing(string user)
        {
            await Clients.Others.SendAsync("UserTyping", user);
        }

        public async Task StopTyping(string user)
        {
            await Clients.Others.SendAsync("UserStoppedTyping", user);
        }
    }

    public class UserStatus
    {
        public string Username { get; set; }
        public bool IsOnline { get; set; }
        public string ConnectionId { get; set; }
    }

    public static class UserManager
    {
        // Thread-safe collection
        private static readonly List<UserStatus> Users = new();

        private static readonly object _lock = new();

        public static void SetOnline(string username, string connectionId)
        {
            lock (_lock)
            {
                var user = Users.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
                if (user != null)
                {
                    user.IsOnline = true;
                    user.ConnectionId = connectionId;
                }
                else
                {
                    Users.Add(new UserStatus
                    {
                        Username = username,
                        IsOnline = true,
                        ConnectionId = connectionId
                    });
                }
            }
        }

        public static void SetOfflineByConnection(string connectionId)
        {
            lock (_lock)
            {
                var user = Users.FirstOrDefault(u => u.ConnectionId == connectionId);
                if (user != null)
                {
                    user.IsOnline = false;
                    user.ConnectionId = null;
                }
            }
        }

        public static List<UserStatus> GetAll()
        {
            lock (_lock)
            {
                return Users.Select(u => new UserStatus
                {
                    Username = u.Username,
                    IsOnline = u.IsOnline
                }).ToList();
            }
        }
    }

}
