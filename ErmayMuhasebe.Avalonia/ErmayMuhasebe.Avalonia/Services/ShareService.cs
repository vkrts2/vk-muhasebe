using System;
using System.IO;
using System.Threading.Tasks;
using ErmayMuhasebe.Repositories;
using ErmayMuhasebe.Services;
using System.Diagnostics;
using Avalonia;
using Avalonia.Input;
using MimeKit;
using MailKit;
using System.Runtime.InteropServices;

namespace ErmayMuhasebe.Avalonia.Services
{
    public class ShareService
    {
        private readonly IUnitOfWork _uow;
        private readonly DatabaseService _dbService;

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const byte VK_CONTROL = 0x11;
        private const byte VK_V = 0x56;
        private const byte VK_RETURN = 0x0D;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        private static void SimulatePasteAndSend()
        {
            try
            {
                // Press Control
                keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
                // Press V
                keybd_event(VK_V, 0, 0, UIntPtr.Zero);
                
                // Release V
                keybd_event(VK_V, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                // Release Control
                keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

                // Wait 1 second for PDF loading animation and rendering
                System.Threading.Thread.Sleep(1000);

                // Press Enter (Send)
                keybd_event(VK_RETURN, 0, 0, UIntPtr.Zero);
                keybd_event(VK_RETURN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            catch { }
        }

        public ShareService(IUnitOfWork uow, DatabaseService dbService)
        {
            _uow = uow;
            _dbService = dbService;
        }

        public async Task ShareViaWhatsAppAsync(string telefon, string mesaj, byte[] pdfBytes, string dosyaAdi)
        {
            try
            {
                // 1. PDF dosyasını geçici klasöre kaydet
                string tempDir = Path.Combine(Path.GetTempPath(), "ErmayShare");
                if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);
                
                string tempPath = Path.Combine(tempDir, dosyaAdi);
                await File.WriteAllBytesAsync(tempPath, pdfBytes);

                // 2. PowerShell kullanarak dosyayı Windows panosuna yerel formatta kopyala (WhatsApp'ın Ctrl+V algılaması için)
                try
                {
                    var psInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -Command \"Set-Clipboard -Path '{tempPath}'\"",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using (var process = Process.Start(psInfo))
                    {
                        process?.WaitForExit();
                    }
                }
                catch (Exception clipEx)
                {
                    Debug.WriteLine($"PowerShell Clipboard Hatası: {clipEx.Message}");
                    
                    // Fallback to Avalonia Clipboard
                    var lifetime = Application.Current?.ApplicationLifetime as global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
                    var clipboard = lifetime?.MainWindow?.Clipboard;
                    if (clipboard != null)
                    {
#pragma warning disable CS0618
                        var dataObject = new DataObject();
                        dataObject.Set(DataFormats.Files, new string[] { tempPath });
                        await clipboard.SetDataObjectAsync(dataObject);
#pragma warning restore CS0618
                    }
                }

                // 3. Dosyanın klasörünü aç ve seç (Kullanıcıyı rahatsız etmemesi için kaldırıldı, dosya panoda kopyalı)
                // if (File.Exists(tempPath))
                // {
                //     Process.Start("explorer.exe", $"/select,\"{tempPath}\"");
                // }

                // 4. WhatsApp Desktop / Web Tetikleme
                // Remove all non-digits (including spaces, dashes, parentheses like "(", ")")
                string digitsOnly = new string(telefon.Where(char.IsDigit).ToArray());
                
                string formattedPhone;
                if (digitsOnly.StartsWith("90") && digitsOnly.Length >= 12)
                {
                    formattedPhone = digitsOnly;
                }
                else if (digitsOnly.StartsWith("0") && digitsOnly.Length == 11)
                {
                    formattedPhone = "90" + digitsOnly.Substring(1);
                }
                else if (digitsOnly.Length == 10)
                {
                    formattedPhone = "90" + digitsOnly;
                }
                else
                {
                    formattedPhone = digitsOnly;
                }

                string encodedMsg = Uri.EscapeDataString(mesaj);
                // UWP WhatsApp has a bug where multiple parameters like &text cause the phone number to be ignored.
                // We pass only the phone number in UWP uri (with '+' sign, no trailing slash after send) to guarantee it focuses on the contact.
                // Using the official HTTPS API endpoint is the most robust way to trigger the local WhatsApp client.
                // Windows and the default browser (Chrome) will catch this link and launch the UWP app focusing the correct contact.
                string whatsappUri = $"https://api.whatsapp.com/send?phone={formattedPhone}";
                string whatsappWebUrl = $"https://web.whatsapp.com/send?phone={formattedPhone}&text={encodedMsg}";

                try
                {
                    // Trigger the official WhatsApp redirect link to launch the local app on the correct contact chat
                    var startInfo = new ProcessStartInfo(whatsappUri)
                    {
                        UseShellExecute = true
                    };
                    Process.Start(startInfo);

                    // Wait 2.5 seconds for UWP WhatsApp to load and focus the chat window
                    await Task.Delay(2500);

                    // Trigger the native clipboard paste and send keyboard event simulation
                    SimulatePasteAndSend();
                }
                catch
                {
                    // Fallback to WhatsApp Web in Chrome/Browser
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = whatsappWebUrl,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WhatsApp Paylaşım Hatası: {ex.Message}");
                throw;
            }
        }

        public async Task SendPdfViaEmailAsync(string aliciEposta, string konu, string mesajBody, byte[] pdfBytes, string dosyaAdi)
        {
            var profil = await _dbService.GetFirmaProfiliAsync();
            if (profil == null || string.IsNullOrWhiteSpace(profil.SmtpHost) || string.IsNullOrWhiteSpace(profil.SmtpUser))
            {
                throw new Exception("Lütfen önce Ayarlar -> Gelişmiş sekmesinden SMTP (E-posta) sunucu ayarlarınızı yapılandırın.");
            }

            // 1. Mail MimeMessage oluştur
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(profil.FirmaAdi ?? "Ermay Muhasebe", profil.SmtpUser));
            message.To.Add(new MailboxAddress("", aliciEposta));
            message.Subject = konu;

            var builder = new BodyBuilder { TextBody = mesajBody };
            builder.Attachments.Add(dosyaAdi, pdfBytes);
            message.Body = builder.ToMessageBody();

            // 2. SMTP ile Mail Gönder
            using (var smtp = new MailKit.Net.Smtp.SmtpClient())
            {
                var options = profil.SmtpSsl ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.StartTls;
                await smtp.ConnectAsync(profil.SmtpHost, profil.SmtpPort, options);
                await smtp.AuthenticateAsync(profil.SmtpUser, profil.SmtpPass);
                await smtp.SendAsync(message);
                await smtp.DisconnectAsync(true);
            }

            // 3. IMAP Entegrasyonu ile Gönderilen Postayı Kaydet
            if (!string.IsNullOrWhiteSpace(profil.ImapHost))
            {
                try
                {
                    using (var imap = new MailKit.Net.Imap.ImapClient())
                    {
                        var imapOptions = profil.ImapSsl ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.StartTls;
                        await imap.ConnectAsync(profil.ImapHost, profil.ImapPort, imapOptions);
                        await imap.AuthenticateAsync(profil.SmtpUser, profil.SmtpPass);

                        var sentFolder = imap.GetFolder(MailKit.SpecialFolder.Sent);
                        if (sentFolder == null)
                        {
                            sentFolder = await imap.GetFolderAsync("Sent") ?? 
                                         await imap.GetFolderAsync("Sent Items") ?? 
                                         await imap.GetFolderAsync("Gönderilenler") ??
                                         await imap.GetFolderAsync("Gönderilen Öyeler");
                        }

                        if (sentFolder != null)
                        {
                            await sentFolder.OpenAsync(MailKit.FolderAccess.ReadWrite);
                            await sentFolder.AppendAsync(message, MessageFlags.Seen);
                        }
                        await imap.DisconnectAsync(true);
                    }
                }
                catch (Exception imapEx)
                {
                    // IMAP hatası mailin gitmesini engellememeli, sadece logla
                    Debug.WriteLine($"IMAP Sent Folder Save Error: {imapEx.Message}");
                }
            }
        }
    }
}
