using Microsoft.AspNetCore.SignalR.Client;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;


namespace ChatClientWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private HubConnection connection;
        private bool isConnected = false;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (isConnected)
            {
                MessageBox.Show("Đã kết nối rồi.");
                return;
            }

            string user = UserNameBox.Text.Trim();
            string ipInput = ServerUrlBox.Text.Trim();
            string serverUrl = $"http://{ipInput}:5262";

            if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(serverUrl))
            {
                MessageBox.Show("Vui lòng nhập đầy đủ tên người dùng và URL server.");
                return;
            }

            connection = new HubConnectionBuilder()
                .WithUrl($"{serverUrl}/chathub?username={Uri.EscapeDataString(user)}")
                .WithAutomaticReconnect()
                .Build();

            connection.On<string, string>("ReceiveMessage", (user, message) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (message.StartsWith("[image]"))
                        AddImageFromUrl(user, message.Substring(7));
                    else if (message.StartsWith("[file]"))
                    {
                        string fileUrl = message.Substring(6);
                        AddFileLink(user, fileUrl);
                    }
                    else
                        AddMessage(user, message);
                });
            });

            connection.On<string>("UserTyping", (user) =>
            {
                Dispatcher.Invoke(() =>
                {
                    TypingStatus.Text = $"{user} đang gõ...";
                });
            });

            connection.On<string>("UserStoppedTyping", (user) =>
            {
                Dispatcher.Invoke(() =>
                {
                    TypingStatus.Text = "";
                });
            });

            connection.On<List<string>>("OnlineUserList", (userList) =>
            {
                Dispatcher.Invoke(() =>
                {
                    UserListPanel.Children.Clear();
                    foreach (var user in userList)
                    {
                        AddUserToOnlineList(user);
                    }
                });
            });

            connection.On<string>("UserOnline", (user) =>
            {
                Dispatcher.Invoke(() => AddUserToOnlineList(user));
            });

            connection.On<string>("UserOffline", (user) =>
            {
                Dispatcher.Invoke(() => RemoveUserFromOnlineList(user));
            });
            connection.On<List<UserStatus>>("UpdateUserList", (users) =>
            {
                Dispatcher.Invoke(() =>
                {
                    UserListPanel.Children.Clear();

                    var sortedUsers = users
                        .OrderByDescending(u => u.IsOnline)
                        .ThenBy(u => u.Username)
                        .ToList();

                    foreach (var user in sortedUsers)
                    {
                        var text = new TextBlock
                        {
                            Text = $"{(user.IsOnline ? "🟢" : "⚪")} {user.Username}",
                            Foreground = Brushes.Black,
                            Margin = new Thickness(2)
                        };
                        UserListPanel.Children.Add(text);
                    }
                });
            });


            try
            {
                await connection.StartAsync();
                await connection.InvokeAsync("Register", user);
                isConnected = true;
                MessageBox.Show("Kết nối thành công!");
                UserNameBox.IsEnabled = false;
                ServerUrlBox.IsEnabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể kết nối server: {ex.Message}");
            }
        }

        private void AddFileLink(string user, string fileUrl)
        {
            bool isMine = user == UserNameBox.Text.Trim();
            string fileName = System.IO.Path.GetFileName(fileUrl);

            var hyperlink = new Hyperlink(new Run($"📎 {fileName}"))
            {
                NavigateUri = new Uri(fileUrl)
            };
            hyperlink.RequestNavigate += async (s, e) =>
            {
                try
                {
                    string savePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName);

                    using var client = new HttpClient();
                    var response = await client.GetAsync(e.Uri);

                    if (response.IsSuccessStatusCode)
                    {
                        await using var fs = new FileStream(savePath, FileMode.Create);
                        await response.Content.CopyToAsync(fs);
                        MessageBox.Show($"Đã tải về: {fileName}");
                    }
                    else
                    {
                        MessageBox.Show("Tải file thất bại");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi tải file: {ex.Message}");
                }
            };

            var stack = new StackPanel();
            if (!isMine)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = user,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.Gray,
                    Margin = new Thickness(0, 0, 0, 3)
                });
            }

            var textBlock = new TextBlock();
            textBlock.Inlines.Add(hyperlink);
            stack.Children.Add(textBlock);

            var bubble = new Border
            {
                Background = isMine ? new SolidColorBrush(Color.FromRgb(204, 229, 255))
                                    : new SolidColorBrush(Color.FromRgb(230, 230, 230)),
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(5),
                Padding = new Thickness(10),
                MaxWidth = 250,
                HorizontalAlignment = isMine ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                Child = stack
            };

            MessagesPanel.Children.Add(bubble);
            ScrollViewer.ScrollToEnd();
        }

        private async void SendLargeFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isConnected) return;

            var dialog = new Microsoft.Win32.OpenFileDialog();
            if (dialog.ShowDialog() != true) return;

            var filePath = dialog.FileName;
            var fileInfo = new FileInfo(filePath);
            var fileStream = File.OpenRead(filePath);

            var content = new MultipartFormDataContent();
            var progressContent = new ProgressableStreamContent(fileStream, 81920, bytesSent =>
            {
                double percent = (double)bytesSent / fileInfo.Length * 100;
                Dispatcher.Invoke(() => ProgressBarUI.Value = percent); // Update UI
            });

            content.Add(progressContent, "file", fileInfo.Name);

            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(10)
            };
            string ip = ServerUrlBox.Text.Trim();
            var response = await client.PostAsync($"http://{ip}:5262/api/fileupload", content);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var fileUrl = System.Text.Json.JsonDocument.Parse(json).RootElement.GetProperty("url").GetString();

                await connection.InvokeAsync("SendMessage", UserNameBox.Text.Trim(), "[file]" + fileUrl);
            }
            else
            {
                MessageBox.Show("Upload file thất bại");
            }
        }

        private void AddUserToOnlineList(string user)
        {
            var item = new TextBlock
            {
                Text = user,
                Name = $"User_{user.Replace(" ", "_")}",
                Margin = new Thickness(0, 2, 0, 2)
            };
            UserListPanel.Children.Add(item);
        }

        private void RemoveUserFromOnlineList(string user)
        {
            var name = $"User_{user.Replace(" ", "_")}";
            var item = UserListPanel.Children.OfType<TextBlock>().FirstOrDefault(x => x.Name == name);
            if (item != null)
            {
                UserListPanel.Children.Remove(item);
            }
        }

        private void AddSystemMessage(string message)
        {
            var text = new TextBlock
            {
                Text = $"🔔 {message}",
                Foreground = Brushes.DarkGray,
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(5, 0, 0, 5)
            };

            MessagesPanel.Children.Add(text);
            ScrollViewer.ScrollToEnd();
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isConnected)
            {
                MessageBox.Show("Vui lòng kết nối server trước.");
                return;
            }

            string user = UserNameBox.Text;
            string message = MessageText.Text;

            if (!string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(message))
            {
                await connection.InvokeAsync("SendMessage", user, message);
                MessageText.Clear();
            }
        }

        private void AddMessage(string user, string message)
        {
            bool isMine = user == UserNameBox.Text.Trim();

            // Tạo nội dung tin nhắn (TextBlock)
            var messageText = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.Black
            };

            // Nếu không phải mình -> thêm tên người gửi bên trên
            StackPanel messageContent = new StackPanel();
            if (!isMine)
            {
                messageContent.Children.Add(new TextBlock
                {
                    Text = user,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.Gray,
                    Margin = new Thickness(0, 0, 0, 3)
                });
            }

            messageContent.Children.Add(messageText);

            // Tạo khung bubble
            var bubble = new Border
            {
                Background = isMine ? new SolidColorBrush(Color.FromRgb(204, 229, 255)) // Xanh nhạt
                                    : new SolidColorBrush(Color.FromRgb(230, 230, 230)), // Xám
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(5),
                Padding = new Thickness(10),
                MaxWidth = 250,
                HorizontalAlignment = isMine ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                Child = messageContent
            };

            MessagesPanel.Children.Add(bubble);

            // Tự động scroll xuống cuối
            ScrollViewer.ScrollToEnd();
        }

        private async void SendImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isConnected)
            {
                MessageBox.Show("Vui lòng kết nối server trước.");
                return;
            }

            string user = UserNameBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(user)) return;

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image Files (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png"
            };

            if (dialog.ShowDialog() == true)
            {
                using var client = new HttpClient();
                var content = new MultipartFormDataContent();
                var fileStream = File.OpenRead(dialog.FileName);
                content.Add(new StreamContent(fileStream), "file", System.IO.Path.GetFileName(dialog.FileName));

                try
                {
                    string ipInput = ServerUrlBox.Text.Trim();
                    var uploadUrl = $"http://{ipInput}:5262/api/imageupload";

                    var response = await client.PostAsync(uploadUrl, content);
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        var imageUrl = System.Text.Json.JsonDocument
                            .Parse(json)
                            .RootElement
                            .GetProperty("url")
                            .GetString();

                        await connection.InvokeAsync("SendMessage", user, "[image]" + imageUrl);
                    }
                    else
                    {
                        MessageBox.Show("Upload ảnh thất bại.");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi gửi ảnh: {ex.Message}");
                }
            }
        }

        private void AddImageFromUrl(string user, string url)
        {
            bool isMine = user == UserNameBox.Text.Trim();

            var image = new Image
            {
                Source = new BitmapImage(new Uri(url)),
                Width = 200,
                Height = 150,
                Stretch = Stretch.UniformToFill,
                Margin = new Thickness(0, 5, 0, 0)
            };

            var stack = new StackPanel();
            if (!isMine)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = user,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.Gray,
                    Margin = new Thickness(0, 0, 0, 3)
                });
            }

            stack.Children.Add(image);

            var bubble = new Border
            {
                Background = isMine ? new SolidColorBrush(Color.FromRgb(204, 229, 255))
                                    : new SolidColorBrush(Color.FromRgb(230, 230, 230)),
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(5),
                Padding = new Thickness(10),
                MaxWidth = 250,
                HorizontalAlignment = isMine ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                Child = stack
            };

            MessagesPanel.Children.Add(bubble);
            ScrollViewer.ScrollToEnd();
        }

        private ImageSource LoadImage(byte[] imageData)
        {
            var bitmap = new BitmapImage();
            using (var ms = new MemoryStream(imageData))
            {
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
            }
            return bitmap;
        }

        private async void MessageText_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!isConnected) return;

            await connection.InvokeAsync("Typing", UserNameBox.Text.Trim());

            typingTimer?.Stop();
            typingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1.5)
            };
            typingTimer.Tick += (s, args) =>
            {
                typingTimer.Stop();
                connection.InvokeAsync("StopTyping", UserNameBox.Text.Trim());
            };
            typingTimer.Start();
        }

        private DispatcherTimer typingTimer;

        private async void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (connection != null && isConnected)
            {
                try
                {
                    await connection.StopAsync();
                    await connection.DisposeAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi ngắt kết nối: {ex.Message}");
                }
            }

            isConnected = false;
            connection = null;

            // Reset giao diện
            MessagesPanel.Children.Clear();      // Xóa lịch sử chat
            UserListPanel.Children.Clear();      // Xóa danh sách online
            TypingStatus.Text = "";              // Xóa trạng thái đang gõ
            ProgressBarUI.Value = 0;

            UserNameBox.IsEnabled = true;
            ServerUrlBox.IsEnabled = true;

            MessageBox.Show("Đã ngắt kết nối");
        }

    }
}