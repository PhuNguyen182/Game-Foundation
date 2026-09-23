# Mobile Notification Service

Local and remote notifications for Android and iOS, built on the Unity Mobile Notifications
package (`com.unity.mobile.notifications` 2.4+). One API for both platforms; everything is a
logged no-op in the Editor and on desktop, so game code needs no platform checks.

## Setup

1. Create a config: **Create > DracoRuan > MobileNotifications > MobileNotificationConfig**.
2. Create an installer: **Create > DracoRuan > MobileNotifications > MobileNotificationInstaller**
   and assign the config. It is picked up by `[AutoInstall]`. Alternatively, register it yourself:

   ```csharp
   protected override void Configure(IContainerBuilder builder)
   {
       builder.AddMobileNotificationService(this.notificationConfig);
   }
   ```

3. Inject `IMobileNotificationService` wherever you need it.
4. Check **Project Settings > Mobile Notifications** (build-time settings the service cannot
   change at runtime):

   | Setting | Needed for |
   |---|---|
   | Android > Reschedule on Device Restart | Keeping scheduled notifications after a reboot |
   | Android > Notification Icons | Custom small/large icons, referenced by id |
   | iOS > Enable Push Notifications | APNs remote notifications and the device token |
   | iOS > Request Authorization on App Launch | Turn off if you ask for permission yourself |

## Permission

```csharp
if (notifications.PermissionStatus == NotificationPermissionStatus.NotRequested)
    await notifications.RequestPermissionAsync();

if (notifications.PermissionStatus == NotificationPermissionStatus.Denied)
    notifications.OpenNotificationSettings(); // The only way back from a denial.
```

Ask at a moment that makes sense to the player rather than at boot; `autoRequestPermission` in
the config exists for projects that do want the prompt at startup. `PermissionStatusChanged` also
fires when the player changes the setting outside the app (noticed when the app regains focus).

## Scheduling and cancelling

```csharp
// One-time, in two hours.
int id = notifications.Schedule(new NotificationData("Energy full", "Come back and play!", 2 * 3600));

// Repeating daily, with a fixed id so it can be replaced or cancelled later.
notifications.Schedule(new NotificationData("Daily reward", "Your reward is ready.", secondsUntil8Pm)
{
    identifier = 1001,
    repeats = true,
    repeatInterval = 24 * 3600,
    androidChannelId = "rewards",
    groupKey = "rewards",
    customData = "{\"screen\":\"daily_reward\"}",
});

notifications.Cancel(1001);            // Scheduled and shown.
notifications.CancelScheduled(id);     // Only if it has not fired yet.
notifications.CancelDisplayed(id);     // Only from the notification center.
notifications.CancelAll();
```

- `identifier = 0` generates an id. Scheduling again under an existing id replaces it.
- `Schedule` returns `NotificationData.InvalidId` when the notification is invalid or the OS
  refuses it; the reason is logged.
- `GetStatus(id)` asks the OS whether a notification is scheduled, delivered or gone.

### Scenarios

A `NotificationScenario` asset (**Create > DracoRuan > MobileNotifications > NotificationScenario**)
holds a series of notifications scheduled together, for example D1/D3/D7 return reminders. Every
entry needs a unique, fixed `identifier`.

```csharp
notifications.ScheduleScenario(returnReminders);                         // Delays count from now.
notifications.ScheduleScenario(returnReminders, lastSessionEndUtc);      // Delays count from an anchor.
notifications.CancelScenario(returnReminders);
```

Scheduling a scenario replaces its previous run. With an anchor, one-time entries whose time has
passed are skipped, and repeating ones move to their next occurrence.

## Receiving

```csharp
notifications.NotificationOpened += OnNotificationOpened;       // Player tapped it (or an action).
notifications.NotificationDelivered += OnNotificationDelivered; // Shown while the app was running.

private void OnNotificationOpened(ReceivedNotification notification)
{
    // Route on your own payload; ActionId is set when an iOS action button was tapped.
    string route = notification.ActionId ?? notification.Data;
}
```

Notifications opened before anyone subscribes are kept and delivered to the first subscriber, so a
router created after boot still receives the notification that launched the app. Each opened
notification is reported once.

## Android

Channels (Android 8.0+) come from the config (`androidDefaultChannel`, `androidChannels`) and can
be managed at runtime:

```csharp
notifications.RegisterChannel(new NotificationChannelData("rewards", "Rewards", "Reward reminders",
    NotificationImportance.High));
notifications.DeleteChannel("old_channel");
```

Android only lets a channel's name and description change after it is created. Importance,
vibration and lights belong to the player from then on; to change them, register a new channel id.

Icons: register them under Project Settings > Mobile Notifications > Notification Icons, then use
their ids in `androidSmallIcon`/`androidLargeIcon` (config defaults) or `smallIcon`/`largeIcon`
(per notification).

## iOS

**Threads** (iOS 12+): notifications with the same `groupKey` stack into one thread (Android
groups them the same way).

**Attachments**: local file URLs of images, audio or video.

```csharp
data.attachmentUrls.Add("file://" + Path.Combine(Application.persistentDataPath, "event.png"));
```

**Actions**: define categories with buttons in the config (`iosCategories`) or at runtime with
`SetCategories`, then reference a category from a notification:

```csharp
data.iosCategoryId = "reward";   // A category with actions "claim" and "later".
// The tap arrives in NotificationOpened with ActionId == "claim" (and UserText for text input).
```

**Remote notifications (APNs)**: enable `iosRegisterForRemoteNotifications` in the config and push
notifications in Project Settings. The device token arrives after the permission request:

```csharp
notifications.DeviceTokenReceived += token => backend.RegisterPushToken(token);
```

The request runs automatically every session once permission is granted, so the token stays fresh.

**Modifying remote notifications in the foreground**: install a handler to rewrite, or suppress,
remote notifications that arrive while the app is running:

```csharp
notifications.SetRemoteNotificationHandler(remote =>
{
    NotificationData local = remote.ToNotificationData();
    local.title = Localize(remote.Title);
    return local; // Return null to show nothing.
});
```

Once installed, interception lasts for the session (an iOS limitation); passing null afterwards
shows remote notifications unchanged.

## Platform notes

- **iOS repeating notifications** cannot have a first delay different from their period. Exactly
  hourly and daily periods are scheduled on the clock (keeping the first fire's time); other
  periods first fire after one period, and a warning is logged.
- **iOS keeps at most 64** pending local notifications; extra ones are silently dropped by the OS.
- **Android remote push** goes through Firebase Cloud Messaging, which is outside this service.
- **Android badges** are derived by the launcher; `ApplicationBadge` is iOS only. `badge` on a
  notification sets the Android notification number where the launcher supports it.
- **Same notification tapped twice**: both platforms keep reporting the last opened notification,
  so tapping the same repeating notification twice in one session is reported once.
