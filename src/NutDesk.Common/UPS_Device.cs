using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;

namespace NutDesk.Common
{
    /// <summary>
    /// Represents a UPS device on a NUT protocol server (upsd). Is the highest-level object for operations in the
    /// NUT protocol. Will not raise exceptions, only events.
    /// </summary>
    /// <remarks>
    /// Connection attempts run on a thread pool thread so that an unreachable server never freezes the user interface.
    /// Every event this class raises is marshalled back onto the thread that constructed it (the UI thread), so
    /// subscribers may touch controls directly.
    /// </remarks>
    public class UPS_Device : IDisposable
    {
        #region Statics/Defaults
        private readonly CultureInfo INVARIANT_CULTURE = CultureInfo.InvariantCulture;
        private const double POWER_FACTOR = 0.8;

        // Delay before the first reconnection attempt. Kept short so a NUT server that is merely restarting is picked
        // up almost immediately.
        private const int RECONNECT_BASE_WAIT_MS = 2000;

        // Upper bound for the exponential back-off between reconnection attempts.
        private const int RECONNECT_MAX_WAIT_MS = 60000;

        private const double DEFAULT_UPDATE_INTERVAL_MS = 1000;

        // How many consecutive failing polls are tolerated on a socket that still claims to be connected before the
        // connection is torn down and rebuilt.
        private const int MAX_CONSECUTIVE_POLL_FAILURES = 3;
        #endregion

        #region Properties

        public string Name => Nut_Config != null ? Nut_Config.UPSName : "null";

        public bool IsConnected => Nut_Socket.ConnectionStatus;

        /// <summary>
        /// True while a connection attempt is running on a background thread.
        /// </summary>
        public bool IsConnecting => Interlocked.CompareExchange(ref connectInProgress, 0, 0) != 0;

        /// <summary>
        /// True while the device is working its way through the reconnection loop: either waiting for the next
        /// scheduled attempt, or performing one.
        /// </summary>
        /// <summary>
        /// True from the moment a reconnection is scheduled until one succeeds or the loop is abandoned. Note it
        /// stays true across the attempt itself, not just while the timer is waiting - the client uses it to keep
        /// repeat failures out of the visible log.
        /// </summary>
        public bool IsReconnecting => Reconnect_Nut.Enabled || reconnectAttempt > 0;

        /// <summary>
        /// The number of the reconnection attempt currently in flight or scheduled. Zero when not reconnecting.
        /// </summary>
        public int ReconnectAttemptNumber => reconnectAttempt;

        public bool IsLoggedIn => Nut_Socket.IsLoggedIn;

        /// <summary>
        /// How often UPS data is updated, in milliseconds.
        /// </summary>
        /// <returns></returns>
        public int PollingInterval => Update_Data.Interval;

        public bool IsUpdatingData
        {
            get => Update_Data.Enabled;
            set
            {
                LogFile.LogTracing("UPS device updating status is now [" + value + "]", LogLvl.LOG_NOTICE, this);
                Update_Data.Enabled = value;
            }
        }

        private UPSData upsData;
        public UPSData UPS_Datas
        {
            get => upsData;
            private set => upsData = value;
        }

        private PowerMethod _PowerCalculationMethod;

        public PowerMethod PowerCalculationMethod => _PowerCalculationMethod;

        #endregion

        #region Events
        public delegate void DataUpdatedEventHandler();
        public event DataUpdatedEventHandler DataUpdated;

        public delegate void ConnectedEventHandler(UPS_Device sender);
        public event ConnectedEventHandler Connected;

        // Notify that the connection was closed gracefully.
        public delegate void DisconnectedEventHandler();
        public event DisconnectedEventHandler Disconnected;

        // Notify of an unexpectedly lost connection.
        public delegate void Lost_ConnectEventHandler();
        public event Lost_ConnectEventHandler Lost_Connect;

        // Error encountered when trying to connect.
        public delegate void ConnectionErrorEventHandler(UPS_Device sender, Exception innerException);
        public event ConnectionErrorEventHandler ConnectionError;

        /// <summary>
        /// Raised each time another reconnection attempt is scheduled. Lets the client distinguish "still trying"
        /// from "gave up", which the <see cref="Disconnected"/> event alone cannot express.
        /// </summary>
        /// <param name="attemptNumber">1 for the first attempt after a lost connection, incrementing thereafter.</param>
        /// <param name="delayMs">How long until that attempt is made.</param>
        public delegate void ReconnectingEventHandler(UPS_Device sender, int attemptNumber, int delayMs);
        public event ReconnectingEventHandler Reconnecting;

        /// <summary>
        /// Raised when the reconnection loop has been abandoned because retrying cannot succeed (bad credentials,
        /// unknown UPS name) or because automatic reconnection is switched off.
        /// </summary>
        public delegate void ReconnectAbandonedEventHandler(UPS_Device sender, string reason);
        public event ReconnectAbandonedEventHandler ReconnectAbandoned;

        /// <summary>
        /// Raised when the NUT server returns an error during normal communication and is deemed important for the client
        /// application to know.
        /// </summary>
        /// <param name="sender">The device object that has received the error.</param>
        /// <param name="nutEx">An exception detailing the error and cirucmstances surrounding it.</param>
        public delegate void EncounteredNUTExceptionEventHandler(UPS_Device sender, NutException nutEx);
        public event EncounteredNUTExceptionEventHandler EncounteredNUTException;

        /// <summary>
        /// Raise an event when a status code is added to the UPS that wasn't there before.
        /// </summary>
        /// <param name="newStatuses">The bitmask of status flags that are currently set on the UPS.</param>
        public delegate void StatusesChangedEventHandler(UPS_Device sender, UPS_States newStatuses);
        public event StatusesChangedEventHandler StatusesChanged;

        #endregion

        // Explicitly the WinForms timer: it ticks on the UI thread, which is what lets event subscribers touch controls
        // directly. System.Threading is imported here too, so the namespace must be spelled out.
        private readonly System.Windows.Forms.Timer Update_Data = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer Reconnect_Nut = new System.Windows.Forms.Timer();

        private Nut_Socket _nutSocket;

        /// <summary>
        /// Stands in for the VB "Private WithEvents Nut_Socket" field: assigning a new socket detaches the handlers
        /// from the old instance and attaches them to the new one, exactly as the Handles clause used to.
        /// </summary>
        private Nut_Socket Nut_Socket
        {
            get => _nutSocket;
            set
            {
                if (_nutSocket != null)
                {
                    _nutSocket.Socket_Broken -= Socket_Broken;
                }

                _nutSocket = value;

                if (_nutSocket != null)
                {
                    _nutSocket.Socket_Broken += Socket_Broken;
                }
            }
        }

        private double Freq_Fallback;
        public Nut_Parameter Nut_Config;
        private readonly Logger LogFile;

        /// <summary>
        /// The synchronization context of the thread that created this object, used to marshal events and timer
        /// manipulation back onto the UI thread from the background connection worker.
        /// </summary>
        private readonly SynchronizationContext uiContext;

        /// <summary>0 when idle, 1 while a connection attempt is in flight. Manipulated through Interlocked.</summary>
        private int connectInProgress;

        /// <summary>Set while a deliberate teardown is running, so it cannot be mistaken for a lost connection.</summary>
        private bool isDisconnecting;

        /// <summary>Set when the client explicitly asked to disconnect, suppressing any further reconnection.</summary>
        private bool disconnectRequested;

        private int reconnectAttempt;
        private bool pollInProgress;
        private int consecutivePollFailures;
        private bool isDisposed;

        /// <summary>Guards against reacting more than once to the same broken connection.</summary>
        private bool socketBreakHandled;

        /// <summary>
        /// Set when the server rejected the connection for a reason retrying cannot fix. Stops
        /// <see cref="SetAutoReconnect"/> from quietly restarting a loop that was deliberately abandoned.
        /// </summary>
        private bool reconnectAbandonedPermanently;

        /// <summary>Carries a non-fatal login failure from the connection worker to the UI thread.</summary>
        private NutException pendingLoginFailure;

        public UPS_Device(Nut_Parameter Nut_Config, Logger LogFile, int pollInterval, int defaultFrequency)
        {
            this.LogFile = LogFile;
            this.Nut_Config = Nut_Config;
            Freq_Fallback = defaultFrequency;
            Nut_Socket = new Nut_Socket(this.Nut_Config, LogFile);

            // Captured here rather than at use time: the worker thread has no context of its own.
            uiContext = SynchronizationContext.Current;

            Reconnect_Nut.Interval = RECONNECT_BASE_WAIT_MS;
            Reconnect_Nut.Enabled = false;
            Reconnect_Nut.Tick += AttemptReconnect;

            Update_Data.Interval = pollInterval > 0 ? pollInterval : Convert.ToInt32(DEFAULT_UPDATE_INTERVAL_MS);
            Update_Data.Enabled = false;
            Update_Data.Tick += Retrieve_UPS_Datas;
        }

        #region Event raising

        /// <summary>
        /// RaiseEvent is a synchronous multicast call, so one throwing subscriber would abort whatever state
        /// transition was in progress. Every notification this class sends goes through a guard like these.
        /// </summary>
        private void SafeRaise(Action action, string description)
        {
            try
            {
                action.Invoke();
            }
            catch (Exception ex)
            {
                LogFile.LogTracing("A subscriber threw while handling the " + description + " notification.",
                                   LogLvl.LOG_ERROR, this);
                LogFile.LogException(ex, this);
            }
        }

        private void RaiseConnected()
        {
            SafeRaise(() => Connected?.Invoke(this), "Connected");
        }

        private void RaiseDisconnected()
        {
            SafeRaise(() => Disconnected?.Invoke(), "Disconnected");
        }

        private void RaiseLostConnect()
        {
            SafeRaise(() => Lost_Connect?.Invoke(), "Lost_Connect");
        }

        private void RaiseConnectionError(Exception ex)
        {
            SafeRaise(() => ConnectionError?.Invoke(this, ex), "ConnectionError");
        }

        private void RaiseNutException(NutException nutEx)
        {
            SafeRaise(() => EncounteredNUTException?.Invoke(this, nutEx), "EncounteredNUTException");
        }

        private void RaiseReconnecting(int attempt, int delayMs)
        {
            SafeRaise(() => Reconnecting?.Invoke(this, attempt, delayMs), "Reconnecting");
        }

        private void RaiseReconnectAbandoned(string reason)
        {
            SafeRaise(() => ReconnectAbandoned?.Invoke(this, reason), "ReconnectAbandoned");
        }

        private void RaiseDataUpdated()
        {
            SafeRaise(() => DataUpdated?.Invoke(), "DataUpdated");
        }

        private void RaiseStatusesChanged(UPS_States newStatuses)
        {
            SafeRaise(() => StatusesChanged?.Invoke(this, newStatuses), "StatusesChanged");
        }

        #endregion

        #region Thread marshalling

        /// <summary>
        /// Run <paramref name="callback"/> on the thread that created this object. Used by the background connection
        /// worker so that WinForms timers and event subscribers are only ever touched from the UI thread.
        /// </summary>
        private void PostToUi(SendOrPostCallback callback, object state = null)
        {
            try
            {
                if (uiContext != null)
                {
                    uiContext.Post(callback, state);
                }
                else
                {
                    // No synchronization context (unit tests, console host): run inline.
                    callback.Invoke(state);
                }
            }
            catch (Exception)
            {
                // The UI is gone - typically the message loop ended while a connection was in flight. Nothing can
                // consume the result, so release the socket here rather than leaking it.
                Interlocked.Exchange(ref connectInProgress, 0);

                try
                {
                    Nut_Socket.Disconnect(true);
                }
                catch
                {
                }
            }
        }

        #endregion

        #region Connection lifecycle

        /// <summary>
        /// Begin connecting to the NUT server. Returns immediately; the outcome is reported through the
        /// <see cref="Connected"/>, <see cref="ConnectionError"/> or <see cref="EncounteredNUTException"/> events.
        /// Whether a failure is retried is decided by <see cref="Nut_Parameter.AutoReconnect"/>, which is read live.
        /// </summary>
        public void Connect_UPS()
        {
            if (isDisposed)
            {
                return;
            }

            // Validate before taking the in-progress flag: a configuration that can never work must not be retried
            // forever, and must not leave the flag held by an attempt that is about to be abandoned.
            var configProblem = DescribeConfigurationProblem();

            if (configProblem != null)
            {
                LogFile.LogTracing(configProblem, LogLvl.LOG_ERROR, this);
                reconnectAbandonedPermanently = true;
                StopReconnecting(configProblem);
                return;
            }

            if (Interlocked.CompareExchange(ref connectInProgress, 1, 0) != 0)
            {
                LogFile.LogTracing("A connection attempt is already in progress; ignoring this request.",
                                   LogLvl.LOG_NOTICE, this);
                return;
            }

            disconnectRequested = false;
            socketBreakHandled = false;
            reconnectAbandonedPermanently = false;
            pendingLoginFailure = null;
            Update_Data.Stop();

            LogFile.LogTracing("Beginning connection: " + Nut_Config.ToString(), LogLvl.LOG_DEBUG, this);

            try
            {
                if (!ThreadPool.QueueUserWorkItem(ConnectWorker))
                {
                    throw new InvalidOperationException("The connection worker could not be queued.");
                }
            }
            catch (Exception ex)
            {
                Interlocked.Exchange(ref connectInProgress, 0);
                LogFile.LogException(ex, this);
                RaiseConnectionError(ex);
                StartReconnectIfEnabled();
            }
        }

        /// <summary>
        /// Describe what is wrong with the current connection settings, or Nothing when they look usable. A blank
        /// host or a UPS name containing spaces produces a protocol error the server repeats forever, so these are
        /// caught here instead of becoming an endless reconnection loop.
        /// </summary>
        private string DescribeConfigurationProblem()
        {
            if (Nut_Config == null)
            {
                return "No NUT server settings are configured.";
            }

            if (string.IsNullOrWhiteSpace(Nut_Config.Host))
            {
                return "No NUT server address is configured. Set one in Settings before connecting.";
            }

            if (Nut_Config.Port <= 0 || Nut_Config.Port > 65535)
            {
                return "The configured NUT server port (" + Nut_Config.Port + ") is not valid.";
            }

            if (string.IsNullOrWhiteSpace(Nut_Config.UPSName))
            {
                return "No UPS name is configured. Set one in Settings before connecting.";
            }

            if (Nut_Config.UPSName.IndexOfAny(new char[] { ' ', '\t' }) >= 0)
            {
                return "The UPS name '" + Nut_Config.UPSName + "' contains a space, which the NUT protocol cannot express.";
            }

            return null;
        }

        /// <summary>
        /// Performs the blocking part of connecting, off the UI thread. Reports its outcome back through
        /// <see cref="PostToUi"/> and never throws.
        /// </summary>
        private void ConnectWorker(object state)
        {
            NutException nutException = null;
            Exception otherException = null;

            try
            {
                try
                {
                    Nut_Socket.Connect();

                    // Confirm the server can actually serve this UPS before declaring success. A upsd that has
                    // accepted the socket but whose driver is not up yet answers DATA-STALE or DRIVER-NOT-CONNECTED.
                    VerifyUpsReady();

                    UPS_Datas = GetUPSProductInfo();

                    // A refused login is NOT fatal: LOGIN only registers this client for FSD notifications, and
                    // monitoring the UPS read-only still works. Reporting it and carrying on is what the client
                    // has always done.
                    if (!string.IsNullOrEmpty(Nut_Config.Login))
                    {
                        try
                        {
                            Nut_Socket.Login();
                        }
                        catch (NutException loginEx)
                        {
                            pendingLoginFailure = loginEx;
                        }
                    }
                }
                catch (NutException ex)
                {
                    nutException = ex;
                }
                catch (Exception ex)
                {
                    otherException = ex;
                }

                if (nutException != null)
                {
                    PostToUi(OnConnectFailedNut, nutException);
                }
                else if (otherException != null)
                {
                    PostToUi(OnConnectFailedTransport, otherException);
                }
                else
                {
                    PostToUi(OnConnectSucceeded);
                }
            }
            finally
            {
                // Last line of defence: whatever happened above, the next attempt must not find the flag stuck.
                // The UI callbacks clear it too; clearing it twice is harmless.
                Interlocked.Exchange(ref connectInProgress, 0);
            }
        }

        /// <summary>
        /// Ask the server for a variable every NUT driver publishes, with no fallback, purely to establish that the
        /// UPS is really being served. Replaces the old heuristic of testing whether the model string came back as
        /// the literal "Unknown", which could not tell a stale server from a UPS that simply has no model variable.
        /// </summary>
        private void VerifyUpsReady()
        {
            LogFile.LogTracing("Verifying the NUT server is ready to serve UPS '" + Name + "'...", LogLvl.LOG_DEBUG, this);

            try
            {
                Nut_Socket.Query_Data("GET VAR " + Name + " ups.status");
                LogFile.LogTracing("UPS '" + Name + "' is being served.", LogLvl.LOG_DEBUG, this);
            }
            catch (NutException nutEx)
            {
                switch (nutEx.LastTransaction.ResponseType)
                {
                    case NUTResponse.ACCESSDENIED:
                    case NUTResponse.UNKNOWNUPS:
                        // The server will not serve this UPS to this client. That is worth failing the connect.
                        throw;

                    case NUTResponse.VARNOTSUPPORTED:
                        // ups.status is expected of every driver, but a UPS that lacks it is still monitorable.
                        LogFile.LogTracing("This UPS does not publish ups.status; continuing without it.",
                                           LogLvl.LOG_WARNING, this);
                        break;

                    default:
                        // DATA-STALE and DRIVER-NOT-CONNECTED mean the driver is not up yet. Connect anyway and
                        // let the poll's own failure budget tear the connection down if it never recovers -
                        // refusing to connect at all would make a transient driver restart look permanent.
                        LogFile.LogTracing("The server answered the readiness probe with " +
                                           nutEx.LastTransaction.ResponseType.ToString() +
                                           "; connecting anyway.", LogLvl.LOG_WARNING, this);
                        break;
                }
            }
        }

        private void OnConnectSucceeded(object state)
        {
            Interlocked.Exchange(ref connectInProgress, 0);

            // A disposal or a disconnect requested while the worker was running takes precedence over its result.
            // The socket must be closed here: this callback is the only thing that still has a reference to it.
            if (isDisposed || disconnectRequested)
            {
                LogFile.LogTracing("Connection succeeded but the device was closed meanwhile; dropping the socket.",
                                   LogLvl.LOG_NOTICE, this);
                try
                {
                    Nut_Socket.Disconnect(true);
                }
                catch (Exception ex)
                {
                    LogFile.LogException(ex, this);
                }
                return;
            }

            if (reconnectAttempt > 0)
            {
                LogFile.LogTracing("Nut Host Reconnected after " + reconnectAttempt + " attempt(s).",
                                   LogLvl.LOG_NOTICE, this);
            }

            reconnectAttempt = 0;
            consecutivePollFailures = 0;
            reconnectAbandonedPermanently = false;
            Reconnect_Nut.Stop();

            RaiseConnected();

            // Report a refused login now that the client is otherwise up: monitoring works, FSD registration does not.
            if (pendingLoginFailure != null)
            {
                LogFile.LogTracing("Connected, but the NUT login was refused. Continuing in monitoring-only mode.",
                                   LogLvl.LOG_WARNING, this);
                var loginFailure = pendingLoginFailure;
                pendingLoginFailure = null;
                RaiseNutException(loginFailure);
            }

            // Have UPS data available right away.
            Retrieve_UPS_Datas(this, null);

            // That first poll may itself have broken the socket; do not start polling a dead connection.
            if (IsConnected)
            {
                Update_Data.Start();
            }
        }

        private void OnConnectFailedTransport(object state)
        {
            Interlocked.Exchange(ref connectInProgress, 0);

            Exception ex = state as Exception;

            if (isDisposed || disconnectRequested)
            {
                try
                {
                    Nut_Socket.Disconnect(true);
                }
                catch
                {
                }
                return;
            }

            CloseSocketQuietly();

            RaiseConnectionError(ex);
            StartReconnectIfEnabled();
        }

        private void OnConnectFailedNut(object state)
        {
            Interlocked.Exchange(ref connectInProgress, 0);

            NutException nutEx = state as NutException;

            if (isDisposed || disconnectRequested)
            {
                try
                {
                    Nut_Socket.Disconnect(true);
                }
                catch
                {
                }
                return;
            }

            CloseSocketQuietly();

            RaiseNutException(nutEx);

            if (IsTerminalNutError(nutEx))
            {
                var reason = "The NUT server rejected this connection (" + nutEx.LastTransaction.ResponseType.ToString() +
                    "). Retrying cannot help, so automatic reconnection has been stopped.";
                LogFile.LogTracing(reason, LogLvl.LOG_ERROR, this);
                StopReconnecting(reason);
                reconnectAbandonedPermanently = true;
            }
            else
            {
                StartReconnectIfEnabled();
            }
        }

        /// <summary>
        /// Errors that will keep happening no matter how many times the client retries, because they describe a
        /// configuration problem rather than a transient server condition.
        /// </summary>
        private static bool IsTerminalNutError(NutException nutEx)
        {
            if (nutEx == null || nutEx.LastTransaction == null)
            {
                return false;
            }

            switch (nutEx.LastTransaction.ResponseType)
            {
                case NUTResponse.ACCESSDENIED:
                case NUTResponse.UNKNOWNUPS:
                case NUTResponse.INVALIDUSERNAME:
                case NUTResponse.INVALIDPASSWORD:
                case NUTResponse.USERNAMEREQUIRED:
                case NUTResponse.PASSWORDREQUIRED:
                case NUTResponse.INVALIDARGUMENT:
                case NUTResponse.UNKNOWNCOMMAND:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Indicates to the NUT server that this client is dependant upon this UPS for power, and registers for a FSD event.
        /// Not usually necessary for normal opeartion (reading UPS variables.)
        /// </summary>
        /// <exception cref="Exception">Any exception raised from <see cref="Nut_Socket.Login()"/></exception>
        public void Login()
        {
            if (!IsConnected || IsLoggedIn)
            {
                throw new InvalidOperationException("UPS is in an invalid state to login.");
            }

            if (!string.IsNullOrEmpty(Nut_Config.Login))
            {
                try
                {
                    Nut_Socket.Login();
                }
                catch (NutException ex)
                {
                    LogFile.LogTracing("Error while attempting to log in.", LogLvl.LOG_ERROR, this);
                    RaiseNutException(ex);
                }
            }
        }

        /// <summary>
        /// Close the connection at the client's request.
        /// </summary>
        /// <param name="cancelReconnect">Also abandon any pending or in-progress reconnection loop.</param>
        /// <param name="forceful">Skip the courtesy LOGOUT command.</param>
        public void Disconnect(bool cancelReconnect = true, bool forceful = false)
        {
            LogFile.LogTracing("Processing request to disconnect...", LogLvl.LOG_DEBUG, this);

            isDisconnecting = true;

            if (cancelReconnect)
            {
                disconnectRequested = true;
                if (Reconnect_Nut.Enabled)
                {
                    LogFile.LogTracing("Stopping Reconnect timer.", LogLvl.LOG_DEBUG, this);
                }
                Reconnect_Nut.Stop();
                reconnectAttempt = 0;
            }

            Update_Data.Stop();

            try
            {
                Nut_Socket.Disconnect(forceful);
            }
            catch (NutException nutEx)
            {
                RaiseNutException(nutEx);
            }
            catch (Exception ex)
            {
                LogFile.LogTracing("Unexpected exception while Disconnecting.", LogLvl.LOG_ERROR, this);
                LogFile.LogException(ex, this);
            }
            finally
            {
                isDisconnecting = false;
                RaiseDisconnected();
            }
        }

        /// <summary>
        /// Tear the socket down without touching the reconnection schedule and without announcing a disconnection to
        /// the client. Used between reconnection attempts, where raising <see cref="Disconnected"/> would knock the
        /// user interface out of its "retrying" state on every single attempt.
        /// </summary>
        private void CloseSocketQuietly()
        {
            isDisconnecting = true;

            try
            {
                if (!isDisposed)
                {
                    Update_Data.Stop();
                }

                Nut_Socket.Disconnect(true);
            }
            catch (Exception ex)
            {
                LogFile.LogTracing("Ignoring error while quietly closing the socket.", LogLvl.LOG_DEBUG, this);
                LogFile.LogException(ex, this);
            }
            finally
            {
                isDisconnecting = false;
            }
        }

        /// <summary>
        /// Abandon the reconnection loop entirely.
        /// </summary>
        public void StopReconnecting(string reason = null)
        {
            var wasRunning = Reconnect_Nut.Enabled || reconnectAttempt > 0;

            Reconnect_Nut.Stop();
            reconnectAttempt = 0;

            if (isDisposed)
            {
                return;
            }

            // Announce it, otherwise the client is left showing a "retrying" icon for a loop that has stopped.
            // A caller that supplied a reason always wants it reported, even if no loop happened to be running.
            if (reason != null)
            {
                RaiseReconnectAbandoned(reason);
            }
            else if (wasRunning)
            {
                RaiseReconnectAbandoned("Automatic reconnection has been stopped; no further attempts will be made.");
            }
        }

        /// <summary>
        /// Change whether this device reconnects automatically, taking effect immediately on the live connection.
        /// Previously this flag was a snapshot taken when the device was created, so toggling it in the UI had no
        /// effect until the next reconnection from scratch.
        /// </summary>
        public void SetAutoReconnect(bool value)
        {
            if (Nut_Config == null || Nut_Config.AutoReconnect == value)
            {
                return;
            }

            LogFile.LogTracing("Automatic reconnection is now [" + value + "]", LogLvl.LOG_NOTICE, this);
            Nut_Config.AutoReconnect = value;

            if (!value)
            {
                StopReconnecting("Automatic reconnection was turned off.");
            }
            else if (reconnectAbandonedPermanently)
            {
                // The server rejected this configuration outright; only an explicit connect should try again.
                LogFile.LogTracing("Not resuming reconnection: the last failure cannot be fixed by retrying.",
                                   LogLvl.LOG_NOTICE, this);
            }
            else if (!IsConnected && !IsConnecting && !disconnectRequested)
            {
                // Turning it back on while disconnected should resume trying.
                StartReconnectIfEnabled();
            }
        }

        #endregion

        #region Reconnection

        /// <summary>
        /// The single place that schedules a reconnection attempt. Routing every caller through here means there is
        /// exactly one definition of "should we reconnect", and the back-off cannot be reset by accident.
        /// </summary>
        private void StartReconnectIfEnabled()
        {
            if (isDisposed || disconnectRequested)
            {
                return;
            }

            if (!Nut_Config.AutoReconnect)
            {
                var reason = "Automatic reconnection is disabled; no further attempts will be made.";
                LogFile.LogTracing(reason, LogLvl.LOG_NOTICE, this);

                Reconnect_Nut.Stop();
                reconnectAttempt = 0;

                // Announced unconditionally: this is the end of the attempt, and the client needs it to put the
                // window back into a disconnected state. Suppressing it after a failed manual connect left the
                // Connect command greyed out with nothing able to re-enable it.
                RaiseReconnectAbandoned(reason);
                return;
            }

            reconnectAttempt += 1;
            var delayMs = ComputeBackoffDelayMs(reconnectAttempt);

            // Assigning Interval while the timer is enabled restarts its period, so stop first to be explicit.
            Reconnect_Nut.Stop();
            Reconnect_Nut.Interval = delayMs;
            Reconnect_Nut.Start();

            LogFile.LogTracing(string.Format("Reconnection attempt {0} scheduled in {1} ms.", reconnectAttempt, delayMs),
                               LogLvl.LOG_NOTICE, this);
            RaiseReconnecting(reconnectAttempt, delayMs);
        }

        /// <summary>
        /// Exponential back-off: 2s, 4s, 8s, 16s, 32s then every 60s. Quick enough to catch a NUT server that is just
        /// restarting, without hammering a server that is down for hours.
        /// </summary>
        private static int ComputeBackoffDelayMs(int attempt)
        {
            if (attempt <= 1)
            {
                return RECONNECT_BASE_WAIT_MS;
            }

            // Cap the exponent before shifting so the multiplication cannot overflow.
            var exponent = Math.Min(attempt - 1, 20);
            double delay = RECONNECT_BASE_WAIT_MS * Math.Pow(2, exponent);

            return Convert.ToInt32(Math.Min(delay, RECONNECT_MAX_WAIT_MS));
        }

        /// <summary>
        /// Fires once per scheduled reconnection attempt. Crucially this no longer cancels its own schedule: the timer
        /// is treated as a one-shot and is re-armed by <see cref="StartReconnectIfEnabled"/> only if the attempt fails.
        /// </summary>
        private void AttemptReconnect(object sender, EventArgs e)
        {
            // One-shot: re-armed on failure, left stopped on success.
            Reconnect_Nut.Stop();

            if (isDisposed || disconnectRequested)
            {
                return;
            }

            if (!Nut_Config.AutoReconnect)
            {
                LogFile.LogTracing("Automatic reconnection was turned off while waiting; abandoning the attempt.",
                                   LogLvl.LOG_NOTICE, this);
                StopReconnecting("Automatic reconnection was turned off.");
                return;
            }

            if (IsConnecting)
            {
                // Another attempt owns the socket right now. Re-arm rather than returning, so the loop can never
                // end silently just because two triggers overlapped.
                LogFile.LogTracing("A connection attempt is already running; rescheduling this tick.",
                                   LogLvl.LOG_DEBUG, this);
                Reconnect_Nut.Interval = ComputeBackoffDelayMs(reconnectAttempt);
                Reconnect_Nut.Start();
                return;
            }

            LogFile.LogTracing(string.Format("Attempting reconnection (attempt {0})...", reconnectAttempt),
                               LogLvl.LOG_NOTICE, this);

            // Discard whatever is left of the previous socket, without announcing a disconnection and without
            // disturbing the reconnection schedule.
            CloseSocketQuietly();

            Connect_UPS();
        }

        #endregion

        #region Socket Interaction

        /// <summary>
        /// Raised by <see cref="Nut_Socket"/>, possibly from the background connection worker. The real work is
        /// marshalled onto the UI thread because it manipulates WinForms timers.
        /// </summary>
        private void Socket_Broken()
        {
            if (isDisposed || isDisconnecting || disconnectRequested)
            {
                // A socket breaking while we are deliberately closing it is not a lost connection.
                return;
            }

            if (IsConnecting)
            {
                // The connection worker owns the outcome of its own attempt and will report the failure itself.
                // Reacting here as well would schedule the retry twice and double-advance the back-off.
                return;
            }

            PostToUi(HandleSocketBroken);
        }

        /// <summary>
        /// Single entry point for reacting to a lost connection. Idempotent: a break can be noticed by more than one
        /// party at once (the socket's own event and the failing poll that provoked it), and both must not each
        /// schedule a reconnection.
        /// </summary>
        private void HandleSocketBroken(object state)
        {
            if (isDisposed || isDisconnecting || disconnectRequested)
            {
                return;
            }

            if (socketBreakHandled)
            {
                LogFile.LogTracing("This broken socket has already been handled; ignoring.", LogLvl.LOG_DEBUG, this);
                return;
            }

            socketBreakHandled = true;

            LogFile.LogTracing("Socket has reported a Broken event.", LogLvl.LOG_WARNING, this);
            Update_Data.Stop();
            consecutivePollFailures = 0;

            // Schedule the retry before telling anyone, so a subscriber that throws cannot leave the device with no
            // connection and no pending attempt.
            StartReconnectIfEnabled();
            RaiseLostConnect();
        }

        #endregion

        /// <summary>
        /// Convenient function to get data that never changes from the UPS.
        /// </summary>
        /// <returns></returns>
        private UPSData GetUPSProductInfo()
        {
            LogFile.LogTracing("Retrieving basic UPS product information...", LogLvl.LOG_NOTICE, this);

            var freshData = new UPSData(
                GetUPSVar(new[] { "ups.mfr", "device.mfr" }, "Unknown")?.Trim(),
                GetUPSVar(new[] { "ups.model", "device.model" }, "Unknown")?.Trim(),
                GetUPSVar(new[] { "ups.serial", "device.serial" }, "Unknown")?.Trim(),
                GetUPSVar("ups.firmware", "Unknown")?.Trim());

            var upsValue = freshData.UPS_Value;
            LogFile.LogTracing("Initializing other well-known UPS variables...", LogLvl.LOG_DEBUG, this);
            try
            {
                var value = float.Parse(GetUPSVar("output.current"), INVARIANT_CULTURE);
                upsValue.Output_Current = value;
                LogFile.LogTracing("output.current: " + value, LogLvl.LOG_DEBUG, this);
            }
            catch (Exception ex)
            {
                if (IsTransportFailure(ex)) throw;
                if (ex.GetType() != typeof(NutException))
                {
                    LogFile.LogException(ex, this);
                }
            }
            try
            {
                var value = float.Parse(GetUPSVar("output.voltage"), INVARIANT_CULTURE);
                upsValue.Output_Voltage = value;
                LogFile.LogTracing("output.voltage: " + value, LogLvl.LOG_DEBUG, this);
            }
            catch (Exception ex)
            {
                if (IsTransportFailure(ex)) throw;
                if (ex.GetType() != typeof(NutException))
                {
                    LogFile.LogException(ex, this);
                }
            }
            try
            {
                var value = float.Parse(GetUPSVar("output.realpower"), INVARIANT_CULTURE);
                upsValue.Output_Power = value;
                LogFile.LogTracing("output.power: " + value, LogLvl.LOG_DEBUG, this);
            }
            catch (Exception ex)
            {
                if (IsTransportFailure(ex)) throw;
            }

            // Determine optimal method for measuring power output from the UPS.
            LogFile.LogTracing("Determining best method to calculate power usage...", LogLvl.LOG_NOTICE, this);
            // Start with directly reading a variable from the UPS.
            try
            {
                // Translator's note: VB's "<> Nothing" on a Double compares against its default value, 0.
                if (freshData.UPS_Value.Output_Power != 0)
                {
                    _PowerCalculationMethod = PowerMethod.RealOutputPower;
                    LogFile.LogTracing("Using RealOutputPower method.", LogLvl.LOG_NOTICE, this);
                }
                else
                {
                    GetUPSVar("ups.realpower");
                    _PowerCalculationMethod = PowerMethod.RealPower;
                    LogFile.LogTracing("Using RealPower method.", LogLvl.LOG_NOTICE, this);
                }
            }
            catch (Exception exRealPower)
            {
                if (IsTransportFailure(exRealPower)) throw;

                try
                {
                    GetUPSVar("ups.realpower.nominal");
                    GetUPSVar("ups.load");
                    _PowerCalculationMethod = PowerMethod.RPNomLoadPct;
                    LogFile.LogTracing("Using RPNomLoadPct method.", LogLvl.LOG_NOTICE, this);
                }
                catch (Exception exNomLoad)
                {
                    if (IsTransportFailure(exNomLoad)) throw;

                    try
                    {
                        GetUPSVar("input.current.nominal");
                        GetUPSVar("input.voltage.nominal");
                        GetUPSVar("ups.load");
                        _PowerCalculationMethod = PowerMethod.InputNomVALoadPct;
                        LogFile.LogTracing("Using InputNomVALoadPct method.", LogLvl.LOG_NOTICE, this);
                    }
                    catch (Exception exInputNom)
                    {
                        if (IsTransportFailure(exInputNom)) throw;

                        if (freshData.UPS_Value.Output_Current.HasValue &&
                                freshData.UPS_Value.Output_Voltage != 0)
                        {
                            _PowerCalculationMethod = PowerMethod.OutputVACalc;
                            LogFile.LogTracing("Using OutputVACalc method.", LogLvl.LOG_NOTICE, this);
                        }
                        else
                        {
                            _PowerCalculationMethod = PowerMethod.Unavailable;
                            LogFile.LogTracing("Unable to find a suitable method to calculate power usage.", LogLvl.LOG_WARNING, this);
                        }
                    }
                }
            }

            // Other constant values for UPS calibration.
            freshData.UPS_Value.Batt_Capacity = double.Parse(GetUPSVar("battery.capacity", -1), INVARIANT_CULTURE);
            Freq_Fallback = double.Parse(GetUPSVar("output.frequency.nominal", Freq_Fallback), INVARIANT_CULTURE);

            LogFile.LogTracing("Completed retrieval of basic UPS product information.", LogLvl.LOG_NOTICE, this);
            return freshData;
        }

        private int oldStatusBitmask;
        private void Retrieve_UPS_Datas(object sender, EventArgs e)
        {
            LogFile.LogTracing("Enter Retrieve_UPS_Datas", LogLvl.LOG_DEBUG, this);

            if (pollInProgress)
            {
                // A poll that overran its interval must not be re-entered by the next tick.
                LogFile.LogTracing("Previous poll still running; skipping this tick.", LogLvl.LOG_DEBUG, this);
                return;
            }

            if (!IsConnected)
            {
                // The poll timer should never be running while disconnected. If it is, recover rather than silently
                // doing nothing forever, which is what the previous implementation did.
                LogFile.LogTracing("Poll fired while disconnected; stopping the poll and starting recovery.",
                                   LogLvl.LOG_WARNING, this);
                Update_Data.Stop();
                HandleSocketBroken(null);
                return;
            }

            pollInProgress = true;

            try
            {
                string UPS_rt_Status = null;

                var upsValue = UPS_Datas.UPS_Value;
                upsValue.Batt_Charge = double.Parse(GetUPSVar("battery.charge", -1), INVARIANT_CULTURE);
                upsValue.Batt_Voltage = double.Parse(GetUPSVar("battery.voltage", -1), INVARIANT_CULTURE);
                upsValue.Batt_Runtime = double.Parse(GetUPSVar("battery.runtime", -1), INVARIANT_CULTURE);
                upsValue.Power_Frequency = double.Parse(GetUPSVar("input.frequency", Freq_Fallback), INVARIANT_CULTURE);
                upsValue.Input_Voltage = double.Parse(GetUPSVar("input.voltage", -1), INVARIANT_CULTURE);
                upsValue.Output_Voltage = double.Parse(GetUPSVar("output.voltage", -1), INVARIANT_CULTURE);
                upsValue.Load = double.Parse(GetUPSVar("ups.load", 0), INVARIANT_CULTURE);

                // Retrieve and/or calculate output power if possible.
                if (_PowerCalculationMethod != PowerMethod.Unavailable)
                {
                    double parsedValue = 0;

                    try
                    {
                        switch (_PowerCalculationMethod)
                        {
                            case PowerMethod.RealPower:
                                parsedValue = double.Parse(GetUPSVar("ups.realpower"), INVARIANT_CULTURE);
                                break;

                            case PowerMethod.RealOutputPower:
                                parsedValue = float.Parse(GetUPSVar("output.realpower"), INVARIANT_CULTURE);
                                break;

                            case PowerMethod.RPNomLoadPct:
                                parsedValue = double.Parse(GetUPSVar("ups.realpower.nominal"), INVARIANT_CULTURE);
                                parsedValue *= UPS_Datas.UPS_Value.Load / 100.0;
                                break;

                            case PowerMethod.InputNomVALoadPct:
                            {
                                var nomCurrent = double.Parse(GetUPSVar("input.current.nominal"), INVARIANT_CULTURE);
                                var nomVoltage = double.Parse(GetUPSVar("input.voltage.nominal"), INVARIANT_CULTURE);

                                parsedValue = nomCurrent * nomVoltage * POWER_FACTOR;
                                parsedValue *= UPS_Datas.UPS_Value.Load / 100.0;
                                break;
                            }
                            case PowerMethod.OutputVACalc:
                                upsValue.Output_Current = float.Parse(GetUPSVar("output.current"), INVARIANT_CULTURE);
                                parsedValue = upsValue.Output_Current.Value * upsValue.Output_Voltage * POWER_FACTOR;
                                break;
                            default:
                                // Should not trigger - something has gone wrong. Deliberately not an
                                // InvalidOperationException, which this class reads as "the socket is dead".
                                throw new NotSupportedException("Reached Else case when attempting to get power output for method " + _PowerCalculationMethod);
                        }
                    }
                    catch (FormatException ex)
                    {
                        LogFile.LogTracing("Unexpected format trying to parse value from UPS. Exception:", LogLvl.LOG_ERROR, this);
                        LogFile.LogTracing(ex.ToString(), LogLvl.LOG_ERROR, this);
                        LogFile.LogTracing("parsedValue: " + parsedValue, LogLvl.LOG_ERROR, this);
                    }
                    catch (Exception ex)
                    {
                        // A dead socket must not be mistaken for an unsupported power variable.
                        if (IsTransportFailure(ex)) throw;
                        LogFile.LogException(ex, this);
                    }

                    // Apply rounding to this number since calculations have extended to three decimal places.
                    // TODO: Remove this round function once gauges can handle decimal places better.
                    upsValue.Output_Power = Math.Round(parsedValue, 1);
                }

                // Handle out-of-range battery charge
                if (upsValue.Batt_Charge < 0 || upsValue.Batt_Charge > 100)
                {
                    if (upsValue.Batt_Voltage > 0)
                    {
                        var nBatt = Math.Floor(upsValue.Batt_Voltage / 12);
                        upsValue.Batt_Charge = Math.Floor((upsValue.Batt_Voltage - (11.6 * nBatt)) / (0.02 * nBatt));
                    }
                    else
                    {
                        LogFile.LogTracing("Unable to calculate UPS Batt_Charge: Batt_Voltage (" + upsValue.Batt_Voltage + ") out of range.", LogLvl.LOG_WARNING, this);
                    }
                }

                // Attempt to calculate battery runtime if not given by the UPS.
                if (upsValue.Batt_Runtime == -1)
                {
                    if (upsValue.Output_Voltage == -1 || upsValue.Batt_Voltage == -1 || upsValue.Batt_Capacity == -1 || upsValue.Batt_Charge == -1)
                    {
                        LogFile.LogTracing("Unable to calculate battery runtime, missing UPS variables.", LogLvl.LOG_WARNING, this);
                        LogFile.LogTracing(string.Format("Output_Voltage: {0}, Batt_Voltage: {1}, Batt_Capacity: {2}, Batt_Charge: {3}",
                            upsValue.Output_Voltage, upsValue.Batt_Voltage, upsValue.Batt_Capacity, upsValue.Batt_Charge), LogLvl.LOG_WARNING, this);
                    }
                    else
                    {
                        double PowerDivider = 0.5;
                        if (upsValue.Load >= 76 && upsValue.Load <= 100)
                        {
                            PowerDivider = 0.4;
                        }
                        else if (upsValue.Load >= 51 && upsValue.Load <= 75)
                        {
                            PowerDivider = 0.3;
                        }

                        upsValue.Load = upsValue.Load != 0 ? upsValue.Load : 0.1;
                        var BattInstantCurrent = (upsValue.Output_Voltage * upsValue.Load) / (upsValue.Batt_Voltage * 100);
                        upsValue.Batt_Runtime = Math.Floor(upsValue.Batt_Capacity * 0.6 * upsValue.Batt_Charge * (1 - PowerDivider) * 3600 / (BattInstantCurrent * 100));
                    }
                }

                // Deliberately no fallback: a server that answers every read with an error must not look like
                // a healthy UPS reporting no status flags at all. A UPS that genuinely has no ups.status is a
                // different matter, and is tolerated.
                try
                {
                    UPS_rt_Status = GetUPSVar("ups.status");
                }
                catch (NutException nutEx) when (nutEx.LastTransaction != null &&
                                                 nutEx.LastTransaction.ResponseType == NUTResponse.VARNOTSUPPORTED)
                {
                    UPS_rt_Status = UPS_States.None.ToString("D");
                }
                // Prepare the status string for Enum parsing by replacing spaces with commas.
                UPS_rt_Status = UPS_rt_Status.Replace(" ", ",");
                try
                {
                    upsValue.UPS_Status = (UPS_States)Enum.Parse(typeof(UPS_States), UPS_rt_Status);
                }
                catch (ArgumentException ex)
                {
                    LogFile.LogTracing("Likely encountered an unknown/invalid UPS status. Using previous status." +
                                Environment.NewLine + ex.Message, LogLvl.LOG_ERROR, this);
                }

                // Get the difference between the old and new statuses, and filter only for active ones.
                var statusDiff = (oldStatusBitmask ^ (int)upsValue.UPS_Status) & (int)upsValue.UPS_Status;

                if (statusDiff == 0)
                {
                    LogFile.LogTracing("UPS statuses have not changed since last update, skipping.", LogLvl.LOG_DEBUG, this);
                }
                else
                {
                    LogFile.LogTracing("UPS statuses have CHANGED...", LogLvl.LOG_NOTICE, this);
                    LogFile.LogTracing("Current statuses: " + UPS_rt_Status, LogLvl.LOG_NOTICE, this);
                    oldStatusBitmask = (int)upsValue.UPS_Status;
                    RaiseStatusesChanged((UPS_States)statusDiff);
                }

                consecutivePollFailures = 0;
                RaiseDataUpdated();

                // Something went wrong while trying to read the data... Consider the socket broken and proceed from here.
            }
            catch (Exception Excep)
            {
                LogFile.LogTracing("Something went wrong in Retrieve_UPS_Datas:", LogLvl.LOG_ERROR, this);
                LogFile.LogException(Excep, this);

                // Only connection-level trouble counts against the budget. A value this client cannot parse is a
                // data problem: it must not tear down and rebuild a perfectly healthy socket once every three
                // polls, forever.
                var countsAsFailure = Excep is NutException || IsTransportFailure(Excep);

                if (!countsAsFailure)
                {
                    LogFile.LogTracing("Treating this as a data error, not a connection failure; polling continues.",
                                       LogLvl.LOG_WARNING, this);
                    return;
                }

                consecutivePollFailures += 1;

                var nutEx = Excep as NutException;
                if (nutEx != null)
                {
                    // The socket is fine but the server cannot serve this UPS - a driver that died, or a upsd that
                    // is mid-restart. Ride out a few polls, then rebuild the connection from scratch.
                    LogFile.LogTracing("The NUT server answered a poll with " +
                                       nutEx.LastTransaction.ResponseType.ToString() + " (failure " +
                                       consecutivePollFailures + " of " + MAX_CONSECUTIVE_POLL_FAILURES + ").",
                                       LogLvl.LOG_WARNING, this);
                    RaiseNutException(nutEx);
                }

                if (!IsConnected)
                {
                    // The socket died under us. Recover instead of silently polling a corpse.
                    Update_Data.Stop();
                    HandleSocketBroken(null);
                }
                else if (consecutivePollFailures >= MAX_CONSECUTIVE_POLL_FAILURES)
                {
                    LogFile.LogTracing(consecutivePollFailures + " consecutive failed polls on a socket that still " +
                                       "reports connected. Forcing a reconnection.", LogLvl.LOG_ERROR, this);
                    CloseSocketQuietly();
                    HandleSocketBroken(null);
                }
            }
            finally
            {
                pollInProgress = false;
            }
        }

        /// <summary>
        /// Distinguishes a failure of the connection itself from a NUT protocol answer. Transport failures must never
        /// be absorbed by a fallback value: doing so is what let a dead connection keep reporting plausible-looking
        /// data (-1, 0, status None) indefinitely without a single error in the log.
        /// </summary>
        private static bool IsTransportFailure(Exception ex)
        {
            if (ex == null)
            {
                return false;
            }

            // EndOfStreamException derives from IOException, and SocketException usually arrives wrapped in one.
            // NullReferenceException is included because a socket field nulled by a concurrent teardown surfaces
            // that way, and it means the same thing: the connection is gone.
            return ex is IOException ||
                   ex is SocketException ||
                   ex is ObjectDisposedException ||
                   ex is InvalidOperationException ||
                   ex is NullReferenceException ||
                   ex is TimeoutException;
        }

        private const int MAX_VAR_RETRIES = 3;
        /// <summary>
        /// Attempts to retrieve the value of a UPS variable using the `GET VAR` NUT API.
        /// </summary>
        /// <param name="varNames">One or more UPS variable name strings to query the server with.</param>
        /// <param name="Fallback_value">Gaurantee a returned value in the event of a NUT protocol error. Transport
        /// failures are always raised, whether or not a fallback was supplied.</param>
        /// <param name="recursing">Special parameter used during DATASTALE error handling.</param>
        /// <exception cref="InvalidOperationException">Attempted to query UPS variable while not <see cref="IsConnected"/>.</exception>
        /// <exception cref="InvalidOperationException">Attempted query with no varNames provided.</exception>
        /// <exception cref="Exception">Any exception raised by <see cref="Nut_Socket.Query_Data"/>, unless <see cref="Fallback_value"/> is given.</exception>
        /// <returns>Example: VAR dummy ups.status "OB HB"</returns>
        public string GetUPSVar(string[] varNames, object Fallback_value = null, bool recursing = false)
        {
            if (varNames == null || varNames.Length == 0)
            {
                throw new InvalidOperationException("Attempted GetUPSVar is no names provided.");
            }

            LogFile.LogTracing($"Attempting to get UPS variable with { varNames.Length - 1 } alternatives.", LogLvl.LOG_DEBUG, this);

            if (!IsConnected)
            {
                throw new InvalidOperationException("Tried to GetUPSVar while disconnected.");
            }

            Transaction Nut_Query = null;
            Exception lastException = null;

            // Try each variable in the array sequentially
            foreach (string varName in varNames)
            {
                try
                {
                    LogFile.LogTracing("Trying variable: " + varName, LogLvl.LOG_DEBUG, this);
                    Nut_Query = Nut_Socket.Query_Data($"GET VAR { Name } { varName }");

                    if (Nut_Query.SplitResponse.Length == 4)
                    {
                        // Extract the variable value from the response.
                        var response = Nut_Query.SplitResponse[3].Trim('"');
                        LogFile.LogTracing("Returning good response: " + response, LogLvl.LOG_DEBUG, this);
                        return response;
                    }
                    else
                    {
                        throw new Exception("Received unexpected response, but no exception was thrown.");
                    }

                }
                catch (Exception ex)
                {
                    lastException = ex;

                    // The connection itself has failed. No fallback can stand in for that, and trying the remaining
                    // alternative names would only produce more of the same.
                    if (IsTransportFailure(ex))
                    {
                        LogFile.LogTracing("Transport failure while retrieving " + varName +
                                           "; the connection is no longer usable.", LogLvl.LOG_ERROR, this);
                        throw;
                    }

                    var nutEx = ex as NutException;
                    if (nutEx != null)
                    {
                        // An if/else chain rather than a switch: the VB "Exit For" below has to leave the loop, and
                        // a C# "break" inside a switch section would only leave the switch.
                        if (nutEx.LastTransaction.ResponseType == NUTResponse.VARNOTSUPPORTED)
                        {
                            LogFile.LogTracing(varName + " is not supported by server, trying next", LogLvl.LOG_WARNING, this);
                            // Continue to next variable
                            continue;
                        }
                        else if (nutEx.LastTransaction.ResponseType == NUTResponse.DATASTALE)
                        {
                            LogFile.LogTracing("DATA-STALE Error Result On Retrieving " + varName + " : " + nutEx.LastTransaction.RawResponse, LogLvl.LOG_ERROR, this);
                            if (recursing)
                            {
                                // Continue to next variable instead of returning Nothing
                                continue;
                            }
                            else
                            {
                                var retryNum = 1;
                                string returnString = null;
                                while (returnString == null && retryNum <= MAX_VAR_RETRIES)
                                {
                                    LogFile.LogTracing("Attempting retry " + retryNum + " to get variable " + varName, LogLvl.LOG_NOTICE, this);
                                    returnString = GetUPSVar(new[] { varName }, Fallback_value, true);
                                    retryNum += 1;
                                }
                                if (returnString != null)
                                {
                                    return returnString;
                                }
                                else
                                {
                                    // Retry failed, continue to next variable
                                    continue;
                                }
                            }
                        }
                        else
                        {
                            LogFile.LogTracing("Unexpected NUT error response when retrieving variable: " + ex.Message, LogLvl.LOG_ERROR, this);
                            break;
                        }
                    }
                    else
                    {
                        LogFile.LogTracing("Unexpected error encountered while retrieving " + varName + ".", LogLvl.LOG_ERROR, this);
                        break;
                    }
                }
            }

            LogFile.LogTracing("Unable to get any UPS variable.", LogLvl.LOG_ERROR, this);

            if (lastException == null)
            {
                LogFile.LogTracing("!! No exceptions were recorded.", LogLvl.LOG_ERROR, this);
                lastException = new InvalidOperationException("No exceptions were recorded by the end of GetUPSVar.");
            }
            else if (lastException as NutException == null)
            {
                // Print exception info for anyting other than NUT errors.
                LogFile.LogTracing("Last exception recorded:", LogLvl.LOG_ERROR, this);
                LogFile.LogException(lastException, this);
            }

            // Translator's note: VB implicitly converted the Object fallback to a String (CStr) here, which uses the
            // current culture for numbers. Convert.ToString does exactly the same.
            var fallbackText = Convert.ToString(Fallback_value);

            if (!string.IsNullOrEmpty(fallbackText))
            {
                LogFile.LogTracing("Returning fallback value.", LogLvl.LOG_NOTICE, this);
                return fallbackText;
            }
            else
            {
                LogFile.LogTracing("No fallback provided. Throwing last exception.", LogLvl.LOG_ERROR, this);
                throw lastException;
            }
        }

        // Overload for backward compatibility with existing code
        public string GetUPSVar(string varName, object Fallback_value = null, bool recursing = false)
        {
            return GetUPSVar(new[] { varName }, Fallback_value, recursing);
        }

        public List<UPS_List_Datas> GetUPS_ListVar()
        {
            var Response = new List<UPS_List_Datas>();
            var Query = "LIST VAR " + Nut_Config.UPSName;

            LogFile.LogTracing("Enter GetUPS_ListVar", LogLvl.LOG_DEBUG, this);

            if (!IsConnected)
            {
                throw new InvalidOperationException("Attempted to list vars while disconnected.");
            }

            try
            {
                var List_Var = Nut_Socket.Query_List_Datas(Query);

                if (List_Var != null)
                {
                    Response = List_Var;
                }
            }
            catch (Exception ex)
            {
                LogFile.LogTracing("Error while listing variables.", LogLvl.LOG_ERROR, this);
                LogFile.LogException(ex, this);

                if (IsTransportFailure(ex) && !IsConnected)
                {
                    HandleSocketBroken(null);
                }

                // Rethrow: the caller shows this to the user. Swallowing it produced an empty variable list with
                // no explanation of why.
                throw;
            }

            return Response;
        }

        /// <summary>
        /// Return instant commands advertised by the current NUT connection. Call from a worker thread because the
        /// network transaction can wait for the socket timeout.
        /// </summary>
        public List<string> GetUPSCommands()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Cannot list UPS commands while disconnected.");
            }

            try
            {
                return Nut_Socket.Query_List_Commands(Name);
            }
            catch (Exception ex)
            {
                LogFile.LogTracing("Error while listing UPS commands.", LogLvl.LOG_ERROR, this);
                LogFile.LogException(ex, this);

                if (IsTransportFailure(ex) && !IsConnected)
                {
                    HandleSocketBroken(null);
                }

                throw;
            }
        }

        public override string ToString()
        {
            return Name;
        }

        #region IDisposable

        /// <summary>
        /// Stops and releases both timers. Without this an abandoned device keeps its WinForms timers alive - they are
        /// rooted by their native timer window - and goes on reconnecting invisibly, to a server the user may have
        /// already changed in the preferences.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            disconnectRequested = true;

            if (disposing)
            {
                try
                {
                    Reconnect_Nut.Tick -= AttemptReconnect;
                    Reconnect_Nut.Stop();
                    Reconnect_Nut.Dispose();
                }
                catch
                {
                }

                try
                {
                    Update_Data.Tick -= Retrieve_UPS_Datas;
                    Update_Data.Stop();
                    Update_Data.Dispose();
                }
                catch
                {
                }

                try
                {
                    Nut_Socket.Disconnect(true);
                }
                catch
                {
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

    }
}
