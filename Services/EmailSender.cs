using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace reportmangerv2.Services;

// Note: This class MUST be registered as a Singleton in Program.cs/Startup.cs
public class EmailSender : IEmailSender, IDisposable
{
    private readonly MailSettings _settings;
    private readonly ILogger<EmailSender> _logger;
    private readonly string _reportsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Reports");

    // Reuse a single SmtpClient instance to avoid connection storms
    private SmtpClient _smtpClient;
    private bool _isConnected;
    
    // Semaphore of 1 ensures thread-safety, as SmtpClient is NOT thread-safe
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    public EmailSender(IOptions<MailSettings> settings, ILogger<EmailSender> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    private async Task EnsureConnectedAsync()
    {
        if (_smtpClient == null)
        {
            _smtpClient = new SmtpClient();
            _smtpClient.ServerCertificateValidationCallback = (sender, certificate, chain, errors) => true; // Remove in production
            _smtpClient.AuthenticationMechanisms.Remove("XOAUTH2");
        }

        if (!_isConnected || !_smtpClient.IsConnected)
        {
            var socketOptions = _settings.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;
            await _smtpClient.ConnectAsync(_settings.Server, _settings.Port, socketOptions);

            if (!string.IsNullOrWhiteSpace(_settings.Username) || !string.IsNullOrWhiteSpace(_settings.Password))
            {
                await _smtpClient.AuthenticateAsync(_settings.Username, _settings.Password);
            }
            _isConnected = true;
        }
    }

    public async Task<bool> SendEmailAsync(
        string reportTitle,
        string to,
        string subject,
        string htmlBody,
        string attachments = null,
        string cc = null)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
        message.To.AddRange(ParseAddresses(to));
        message.Subject = subject;

        if (!string.IsNullOrWhiteSpace(cc))
        {
            message.Cc.AddRange(ParseAddresses(cc));
            _logger.LogInformation("Email CC: {CcRecipients}", cc);
        }

        var bodyBuilder = new BodyBuilder { HtmlBody = htmlBody };

        if (!string.IsNullOrWhiteSpace(attachments))
        {
            var files = attachments.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var file in files)
            {
                var fileName = file.Trim();
                var filePath = Path.Combine(_reportsDirectory, Path.GetFileName(fileName));
                if (File.Exists(filePath))
                {
                    bodyBuilder.Attachments.Add(filePath);
                }
                else
                {
                    _logger.LogWarning("Attachment not found: {AttachmentPath}", filePath);
                }
            }
        }

        message.Body = bodyBuilder.ToMessageBody();

        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            await _connectionLock.WaitAsync();
            try
            {
                await EnsureConnectedAsync();
                await _smtpClient.SendAsync(message);
                _logger.LogInformation("Email sent successfully to {Recipients}.", to);
                return true;
            }
            catch (Exception ex)
                        
            {
                _logger.LogWarning(ex, "Attempt {Attempt} to send email failed.", attempt);
                
                // Force reconnect on next attempt in case the server dropped the connection
                _isConnected = false; 

                // Check if this is specifically a rate-limiting error
                bool isRateLimited = ex is SmtpCommandException smtpEx && 
                                     (smtpEx.Message.Contains("4.4.2") || smtpEx.Message.Contains("rate", StringComparison.OrdinalIgnoreCase));

                if (isRateLimited)
                {
                    _logger.LogWarning("Server rate limit hit. Applying extended backoff.");
                }

                if (attempt == maxAttempts)
                {
                    _logger.LogError(ex, $"Failed to send email for {reportTitle} after {maxAttempts} attempts. Giving up.", maxAttempts);
                    return false; // Email is officially dropped
                }

                // Exponential backoff: 5s, then 15s, then 30s (longer if rate limited)
                int delaySeconds = isRateLimited ? 15 * attempt : 5 * attempt;
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
            }
            finally
            {
                _connectionLock.Release();
            }
            
        }

        return false;
    }

    private static IEnumerable<MailboxAddress> ParseAddresses(string addresses)
    {
        return addresses
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(addr => MailboxAddress.Parse(addr.Trim()));
    }

    public void Dispose()
    {
        if (_smtpClient != null)
        {
            try
            {
                if (_smtpClient.IsConnected)
                {
                    _smtpClient.Disconnect(true);
                }
                _smtpClient.Dispose();
            }
            catch
            {
                // Ignore disposal errors
            }
        }
        _connectionLock.Dispose();
    }
}



// using System;
// using Microsoft.Extensions.Options;
// using MimeKit;
// using MailKit.Net.Smtp;
// using MailKit.Security;

// namespace reportmangerv2.Services;

// public class EmailSender : IEmailSender
// {

//     private static readonly SemaphoreSlim _smtpSemaphore = new(5);
//     private readonly MailSettings _settings;
//     private readonly string _reportsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Reports");
//     private readonly ILogger<EmailSender> _logger;

//     public EmailSender(IOptions<MailSettings> settings, ILogger<EmailSender> logger)
//     {
//         _settings = settings.Value;
//         _logger = logger;
//     }

//     public async Task<bool> SendEmailAsync(
//         string to,
//         string subject,
//         string htmlBody,
//         string attachments = null,
//         string cc = null
//     )
//     {
//         var message = new MimeMessage();
//         message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
//         message.To.AddRange(ParseAddresses(to));
//         message.Subject = subject;

//         if (!string.IsNullOrWhiteSpace(cc))
//         {
//             message.Cc.AddRange(ParseAddresses(cc));
//             _logger.LogInformation("Email CC: {CcRecipients}", cc);
//         }

//         var bodyBuilder = new BodyBuilder { HtmlBody = htmlBody };

//         // Handle multiple attachments
//         if (!string.IsNullOrWhiteSpace(attachments))
//         {
//             var files = attachments.Split(',', StringSplitOptions.RemoveEmptyEntries);
//             foreach (var file in files)
//             {
//                 var fileName = file.Trim();
//                 var filePath = Path.Combine(_reportsDirectory, Path.GetFileName(fileName));
//                 if (File.Exists(filePath))
//                 {
//                     bodyBuilder.Attachments.Add(filePath);
//                 }
//                 else
//                 {
//                     _logger.LogWarning("Attachment not found: {AttachmentPath}", filePath);
//                 }
//             }
//         }

//         message.Body = bodyBuilder.ToMessageBody();
//         /*

//          using (var client = new SmtpClient())
//                 {

//                     client.ServerCertificateValidationCallback = (s, c, h, e) => true; // For testing only
//                    // await client.ConnectAsync(_smtpServer,_smtpPort,true,CancellationToken.None);
//                     client.AuthenticationMechanisms.Remove("XOAUTH2");
//                     client.Connect(_smtpServer, _smtpPort, SecureSocketOptions.StartTls);
//                     try
//                     {
//                        //var ntlm = new SaslMechanismNtlm("EGYPTPOST\\87642","FathySelim$1945"); 
//                      //  var ntlm = new SaslMechanismNtlm("EGYPTPOST\\87462","weal"); 
//                         //sm02-dpp-8xg\post

//                     client.Authenticate(_username,_password);
//                    //client.Authenticate(ntlm);
//                     client.Send(message);
//                     }catch( Exception ex)
//                     {
//                      Console.WriteLine(ex.Message);

//                     }
//                     finally
//                     {

//                     client.Disconnect(true);
//                     }

//                 }



//         */

//         var socketOptions = _settings.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;
//         const int maxAttempts = 3;

//         for (var attempt = 1; attempt <= maxAttempts; attempt++)
//         {
//             await _smtpSemaphore.WaitAsync();
//             try
//             {
//                 using var client = new SmtpClient();
//                 client.ServerCertificateValidationCallback = (sender, certificate, chain, errors) => true; // For testing only; remove in production.
//                 client.AuthenticationMechanisms.Remove("XOAUTH2");

//                 await client.ConnectAsync(_settings.Server, _settings.Port, socketOptions);

//                 if (!string.IsNullOrWhiteSpace(_settings.Username) || !string.IsNullOrWhiteSpace(_settings.Password))
//                 {
//                     await client.AuthenticateAsync(_settings.Username, _settings.Password);
//                 }

//                 await client.SendAsync(message);
//                 await client.DisconnectAsync(true);

//                 _logger.LogInformation("Email sent successfully to {Recipients}.", to);
//                 return true;
//             }
//             catch (Exception ex)
//             {
//                 _logger.LogWarning(ex, "Attempt {Attempt} to send email failed.", attempt);

//                 if (attempt == maxAttempts)
//                 {
//                     _logger.LogError(ex, "Failed to send email after {MaxAttempts} attempts.", maxAttempts);
//                     return false;
//                 }

//                 await Task.Delay(TimeSpan.FromSeconds(5));
//             }
//             finally
//             {
//                 _smtpSemaphore.Release();
//             }
//         }

//         return false;



//     }

//     private static IEnumerable<MailboxAddress> ParseAddresses(string addresses)
//     {
//         return addresses
//             .Split(',', StringSplitOptions.RemoveEmptyEntries)
//             .Select(addr => MailboxAddress.Parse(addr.Trim()));
//     }


// }
