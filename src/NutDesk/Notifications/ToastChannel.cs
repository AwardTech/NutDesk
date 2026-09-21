using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Toolkit.Uwp.Notifications;
using NutDesk.Common;

namespace NutDesk.Notifications
{
    /// <summary>
    /// Shows notifications in the Windows notification center, or as a tray balloon where that is not available.
    /// </summary>
    /// <remarks>
    /// Every call that touches the toast library sits in a method of its own marked NoInlining. The library binds
    /// to WinRT types, and on a system without them the failure surfaces when the calling method is compiled - so
    /// it must be a method whose caller can still catch the exception and fall back to the balloon.
    /// </remarks>
    internal sealed class ToastChannel : IDisposable
    {
        private const string ACTION_ARGUMENT = "action";
        private const string ACTION_OPEN = "open";

        private readonly Logger logger;
        private readonly SynchronizationContext uiContext;
        private readonly Action showMainWindow;
        private readonly Action<string, string, bool> balloonFallback;
        private readonly CultureInfo uiCulture;

        private bool activationHooked;
        private volatile bool isDisposed;

        internal ToastChannel(Logger logger, SynchronizationContext uiContext, CultureInfo uiCulture,
                              Action showMainWindow, Action<string, string, bool> balloonFallback)
        {
            this.uiCulture = uiCulture;
            this.logger = logger;
            this.uiContext = uiContext;
            this.showMainWindow = showMainWindow;
            this.balloonFallback = balloonFallback;

            if (!IsPlatformSupported)
            {
                logger.LogTracing("Windows toast notifications need Windows 10 or later; tray balloons will be used.",
                                  LogLvl.LOG_NOTICE, this);
                return;
            }

            try
            {
                HookActivation();
                activationHooked = true;
            }
            catch (Exception ex)
            {
                // Toasts can still be shown; clicking them just will not bring the window forward.
                logger.LogTracing("Unable to listen for toast activations.", LogLvl.LOG_WARNING, this);
                logger.LogException(ex, this);
            }
        }

        /// <summary>
        /// Toasts for desktop applications arrived with Windows 10. The manifest declares Windows 10 support, so the
        /// reported version is the real one.
        /// </summary>
        internal static bool IsPlatformSupported
        {
            get
            {
                var os = Environment.OSVersion;
                return os.Platform == PlatformID.Win32NT && os.Version >= new Version(10, 0, 10240);
            }
        }

        /// <summary>Shows one event. Never throws.</summary>
        /// <param name="persistCritical">Keep critical kinds on screen until the user dismisses them.</param>
        internal void Show(NotificationEvent notification, bool persistCritical)
        {
            if (isDisposed || notification == null)
            {
                return;
            }

            var critical = NotificationKinds.IsCritical(notification.Kind);

            if (!IsPlatformSupported)
            {
                ShowBalloon(notification.Title, notification.Body, critical);
                return;
            }

            try
            {
                ShowToast(notification, critical && persistCritical);
            }
            catch (Exception ex)
            {
                // Toasts depend on the shell, on Focus Assist policies and on a registration Windows may have lost;
                // none of that is a reason for the user not to hear about the UPS.
                logger.LogTracing("Unable to show a toast notification; falling back to a tray balloon.",
                                  LogLvl.LOG_WARNING, this);
                logger.LogException(ex, this);
                ShowBalloon(notification.Title, notification.Body, critical);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ShowToast(NotificationEvent notification, bool stayOnScreen)
        {
            var builder = new ToastContentBuilder()
                .AddArgument(ACTION_ARGUMENT, ACTION_OPEN)
                .AddText(notification.Title)
                .AddText(notification.Body);

            if (stayOnScreen)
            {
                // A Reminder stays until dismissed, which is the point for a power failure noticed on returning to
                // the desk. Windows only honours the scenario when the toast has a button, hence the two below.
                builder.SetToastScenario(ToastScenario.Reminder)
                       .AddButton(new ToastButton()
                                      .SetContent(NotificationText.Get("Notif_Action_Open", uiCulture))
                                      .AddArgument(ACTION_ARGUMENT, ACTION_OPEN))
                       .AddButton(new ToastButtonDismiss());
            }

            var topic = NotificationKinds.Topic(notification.Kind);

            // Windows replaces a toast only when both tag and group match, so both carry the topic.
            CustomizeToast customize = toast =>
            {
                toast.Tag = topic;
                toast.Group = topic;
            };

            builder.Show(customize);
        }

        private void ShowBalloon(string title, string text, bool isCritical)
        {
            logger.LogTracing("Showing the notification as a tray balloon.", LogLvl.LOG_DEBUG, this);
            RunOnUi(() => balloonFallback?.Invoke(title, text, isCritical), "tray balloon");
        }

        #region Activation

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void HookActivation()
        {
            ToastNotificationManagerCompat.OnActivated += OnToastActivated;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void UnhookActivation()
        {
            ToastNotificationManagerCompat.OnActivated -= OnToastActivated;
        }

        /// <summary>
        /// Raised by the toast library on a background thread whenever one of our toasts, or its button, is clicked.
        /// </summary>
        private void OnToastActivated(ToastNotificationActivatedEventArgsCompat e)
        {
            if (isDisposed)
            {
                return;
            }

            // Every toast this application shows is about the UPS, and the only useful response to clicking one is
            // to show the window that describes it - whatever the argument says.
            logger.LogTracing("A notification was clicked; bringing NutDesk forward.", LogLvl.LOG_DEBUG, this);
            RunOnUi(() =>
            {
                if (!isDisposed)
                {
                    showMainWindow?.Invoke();
                }
            }, "show main window");
        }

        #endregion

        /// <summary>
        /// Runs <paramref name="action"/> on the UI thread without waiting for it. Posting rather than sending: the
        /// callers are event handlers and the outbox, and none of them may ever block on a busy UI thread.
        /// </summary>
        private void RunOnUi(Action action, string description)
        {
            SendOrPostCallback guarded = _ =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    logger.LogTracing("The " + description + " callback failed.", LogLvl.LOG_WARNING, this);
                    logger.LogException(ex, this);
                }
            };

            try
            {
                if (uiContext != null)
                {
                    uiContext.Post(guarded, null);
                }
                else
                {
                    // No UI context was captured (the center was built off the UI thread). Running inline is the
                    // only option left, and the guard keeps any cross-thread complaint from escaping.
                    guarded(null);
                }
            }
            catch (Exception ex)
            {
                // The message loop has ended: the application is closing and there is nobody left to show it to.
                logger.LogTracing("Unable to reach the UI thread for the " + description + ".", LogLvl.LOG_DEBUG, this);
                logger.LogException(ex, this);
            }
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;

            if (activationHooked)
            {
                try
                {
                    UnhookActivation();
                }
                catch (Exception ex)
                {
                    logger.LogException(ex, this);
                }
            }
        }
    }
}
