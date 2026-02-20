using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notification.Domain.Abstractions;
using Notification.Domain.Models;

// Alias to avoid collision with our root 'Notification' namespace
using FcmNotification = FirebaseAdmin.Messaging.Notification;

namespace Notification.Providers.Push.Firebase;

/// <summary>
/// Sends push notifications via Firebase Cloud Messaging (FCM).
/// Supports per-tenant credentials by lazily creating named FirebaseApp instances.
/// </summary>
public class FirebasePushProvider : INotificationProvider
{
    public string ProviderName => "Firebase";
    public NotificationChannel Channel => NotificationChannel.Push;

    private readonly FirebaseOptions _defaultOptions;
    private readonly ILogger<FirebasePushProvider> _logger;
    private readonly Dictionary<string, FirebaseApp> _appCache = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public FirebasePushProvider(
        IOptions<FirebaseOptions> options,
        ILogger<FirebasePushProvider> logger)
    {
        _defaultOptions = options.Value;
        _logger = logger;
    }

    public async Task<string?> SendAsync(
        NotificationRequest request,
        RenderedTemplate template,
        TenantConfig tenantConfig,
        CancellationToken cancellationToken = default)
    {
        var app = await GetOrCreateAppAsync(tenantConfig);
        var messaging = FirebaseMessaging.GetMessaging(app);

        var message = new Message
        {
            Token = request.Recipient,
            Notification = new FcmNotification
            {
                Title = template.Subject,
                Body = template.TextBody
            },
            Data = template.Data
        };

        var messageId = await messaging.SendAsync(message, cancellationToken);

        _logger.LogDebug(
            "Push sent via Firebase to {Token}, FCM msgId={MessageId}",
            request.Recipient[..Math.Min(10, request.Recipient.Length)] + "…",
            messageId);

        return messageId;
    }

    private async Task<FirebaseApp> GetOrCreateAppAsync(TenantConfig tenantConfig)
    {
        var appName = $"tenant-{tenantConfig.TenantId}";

        await _lock.WaitAsync();
        try
        {
            if (_appCache.TryGetValue(appName, out var existing))
                return existing;

            var credentialFile = tenantConfig.ProviderSettings.TryGetValue("Firebase:CredentialFile", out var cf)
                ? cf
                : _defaultOptions.CredentialFile;

            var credential = GoogleCredential
                .FromFile(credentialFile)
                .CreateScoped("https://www.googleapis.com/auth/firebase.messaging");

            var app = FirebaseApp.Create(new AppOptions { Credential = credential }, appName);
            _appCache[appName] = app;

            _logger.LogInformation(
                "Created FirebaseApp '{AppName}' for tenant {TenantId}",
                appName, tenantConfig.TenantId);

            return app;
        }
        finally
        {
            _lock.Release();
        }
    }
}
