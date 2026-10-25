using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace NutDesk.Common
{
    /// <summary>
    /// Manages low-level interaction with an endpoint communicating in the NUT protocol (upsd).
    /// Passes up most encountered exceptions, while resetting its state if necessary.
    /// </summary>
    public class Nut_Socket
    {
        /// <summary>
        /// How long to wait for a read or a write on an established socket before giving up.
        /// </summary>
        private const int TIMEOUT_MS = 5000;

        /// <summary>
        /// How long to wait for the TCP handshake (including DNS resolution) to complete. Without this bound the
        /// operating system decides, which is ~21 seconds for a silently dropped SYN.
        /// </summary>
        private const int CONNECT_TIMEOUT_MS = 5000;

        /// <summary>
        /// How long <see cref="Disconnect"/> waits for an in-flight query before closing the socket outright.
        /// </summary>
        private const int DISCONNECT_LOCK_WAIT_MS = 250;

        private readonly Encoding NUT_CHARENCODING = Encoding.ASCII;

        #region Properties
        /// <summary>
        /// Whether this object believes it holds a usable connection. Note that <see cref="TcpClient.Connected"/> only
        /// reflects the outcome of the last I/O operation, so this can still report True for a connection that has
        /// silently died. A failed query is what actually moves this to False, via <see cref="OnSocketBroken"/>.
        /// </summary>
        public bool ConnectionStatus
        {
            get
            {
                // Read the field once: the connection worker can null it between the two tests.
                var activeClient = client;
                return _isConnected && activeClient != null && activeClient.Connected;
            }
        }

        private bool _isLoggedIn = false;
        public bool IsLoggedIn => _isLoggedIn;

        public string NUTVersion { get; private set; }
        public string NetVersion { get; private set; }
        #endregion

        private Logger LogFile;
        private Nut_Parameter NutConfig;

        // Socket Variables
        private TcpClient client;
        private NetworkStream NutStream;
        private StreamReader ReaderStream;
        private StreamWriter WriterStream;

        /// <summary>
        /// Set by this class when a connection has been fully established, and cleared by every teardown path. Unlike
        /// <see cref="TcpClient.Connected"/> this is never a stale cache of a previous socket.
        /// </summary>
        private bool _isConnected;

        /// <summary>
        /// Serialises a complete query/response exchange. This replaces the former streamInUse boolean, which was
        /// released before the response was read and could be left set forever if a LIST response threw.
        /// </summary>
        private readonly object streamLock = new object();

        /// <summary>
        /// Ensures a single broken socket raises <see cref="Socket_Broken"/> exactly once.
        /// </summary>
        private bool socketBrokenRaised;

        /// <summary>
        /// Incremented by every teardown. <see cref="Connect"/> captures it before the handshake and refuses to
        /// publish its socket if the value has moved on, which is what stops a connection attempt that finishes
        /// after the caller gave up from installing a live session nobody will ever close.
        /// </summary>
        private int connectGeneration;

        public delegate void Socket_BrokenEventHandler();
        public event Socket_BrokenEventHandler Socket_Broken;

        public Nut_Socket(Nut_Parameter Nut_Config, Logger logger)
        {
            LogFile = logger;
            NutConfig = Nut_Config;
        }

        public void Connect()
        {
            // TODO: Use LIST UPS protocol command to get valid UPSs.
            string Host = NutConfig.Host;
            int Port = NutConfig.Port;

            // Translator's note: the VB source read "If String.IsNullOrEmpty(Host) Or IsNothing(Port)". Port is an
            // Integer, so IsNothing(Port) was always False and the test reduces to the host check.
            if (string.IsNullOrEmpty(Host))
            {
                throw new InvalidOperationException("Host and Port must be specified to connect.");
            }

            int generation;

            lock (streamLock)
            {
                // Never build on top of the remains of a previous connection.
                CleanupSocket();
                generation = connectGeneration;
            }

            LogFile.LogTracing(string.Format("Attempting TCP socket connection to {0}:{1} (timeout {2} ms)...",
                                             Host, Port, CONNECT_TIMEOUT_MS), LogLvl.LOG_NOTICE, this);

            TcpClient newClient = null;

            try
            {
                newClient = new TcpClient()
                {
                    SendTimeout = TIMEOUT_MS,
                    ReceiveTimeout = TIMEOUT_MS
                };

                // BeginConnect resolves the host name and performs the handshake without blocking indefinitely, so an
                // unreachable server costs CONNECT_TIMEOUT_MS instead of the operating system's ~21 second default.
                IAsyncResult connectResult = newClient.BeginConnect(Host, Port, null, null);

                if (!connectResult.AsyncWaitHandle.WaitOne(CONNECT_TIMEOUT_MS, false))
                {
                    try
                    {
                        newClient.Close();
                    }
                    catch
                    {
                    }
                    throw new TimeoutException(string.Format("Timed out after {0} ms connecting to {1}:{2}.",
                                                             CONNECT_TIMEOUT_MS, Host, Port));
                }

                // Surfaces the real reason (connection refused, host unreachable, DNS failure...).
                newClient.EndConnect(connectResult);

                // Publish the finished socket in one atomic step, and only if nobody tore this object down while
                // the handshake was running on the worker thread.
                lock (streamLock)
                {
                    if (generation != connectGeneration)
                    {
                        throw new InvalidOperationException(
                            "The connection was cancelled while it was being established.");
                    }

                    client = newClient;
                    newClient = null;

                    NutStream = client.GetStream();
                    NutStream.ReadTimeout = TIMEOUT_MS;
                    NutStream.WriteTimeout = TIMEOUT_MS;
                    ReaderStream = new StreamReader(NutStream, NUT_CHARENCODING);
                    WriterStream = new StreamWriter(NutStream, NUT_CHARENCODING);

                    _isConnected = true;
                    socketBrokenRaised = false;
                }

                LogFile.LogTracing("Connection established and streams ready.", LogLvl.LOG_NOTICE, this);
                LogFile.LogTracing("Gathering basic info about the NUT server...", LogLvl.LOG_DEBUG, this);

                try
                {
                    // Response: Network UPS Tools upsd 2.8.1 - https://www.networkupstools.org/
                    var Nut_Query = Query_Data("VER");
                    NUTVersion = Nut_Query.RawResponse;
                    LogFile.LogTracing("Server version: " + NUTVersion, LogLvl.LOG_NOTICE, this);
                }
                catch (NutException nutEx)
                {
                    LogFile.LogTracing("Error retrieving server version.", LogLvl.LOG_WARNING, this);
                    LogFile.LogException(nutEx, this);
                }

                try
                {
                    var Nut_Query = Query_Data("NETVER");
                    NetVersion = Nut_Query.RawResponse;
                    LogFile.LogTracing("Protocol version: " + NetVersion, LogLvl.LOG_NOTICE, this);
                }
                catch (NutException nutEx)
                {
                    LogFile.LogTracing("Error retrieving protocol version.", LogLvl.LOG_WARNING, this);
                    LogFile.LogException(nutEx, this);
                }
            }
            catch (Exception ex)
            {
                LogFile.LogTracing("Error connecting socket.", LogLvl.LOG_DEBUG, this);
                LogFile.LogException(ex, this);

                if (newClient != null)
                {
                    try
                    {
                        newClient.Close();
                    }
                    catch
                    {
                    }
                }

                lock (streamLock)
                {
                    // Only clean up if this attempt still owns the object, so one attempt's failure cannot destroy
                    // a newer attempt's socket.
                    if (generation == connectGeneration)
                    {
                        CleanupSocket();
                    }
                }

                throw;
            }

            LogFile.LogTracing("Completed gathering basic info about NUT server.", LogLvl.LOG_DEBUG, this);
        }

        public void Login()
        {
            if (_isLoggedIn)
            {
                throw new InvalidOperationException("Attempted to login when already logged in.");
            }

            LogFile.LogTracing(string.Format("Logging in to UPS [{0}] as user [{1}] ({2})...",
                                NutConfig.UPSName, NutConfig.Login,
                                string.IsNullOrEmpty(NutConfig.Password)
                                    ? "NO Password" : "Password provided"), LogLvl.LOG_NOTICE, this);

            if (!string.IsNullOrEmpty(NutConfig.Login))
            {
                Query_Data("USERNAME " + NutConfig.Login);

                if (!string.IsNullOrEmpty(NutConfig.Password))
                {
                    Query_Data("PASSWORD " + NutConfig.Password);
                }
            }

            Query_Data("LOGIN " + NutConfig.UPSName);
            _isLoggedIn = true;
            LogFile.LogTracing("Authenticated successfully.", LogLvl.LOG_NOTICE, this);
        }

        /// <summary>
        /// Perform various functions necessary to disconnect the socket from the NUT server. This method never throws:
        /// a disconnect that fails half way through would otherwise leave the object in an inconsistent state and,
        /// worse, could raise <see cref="Socket_Broken"/> and restart a reconnection loop the caller just cancelled.
        /// </summary>
        /// <param name="skipLogout">Do not send the LOGOUT command to the NUT server.</param>
        public void Disconnect(bool skipLogout = false)
        {
            // Do not queue behind an in-flight query: a read can sit on the socket for up to TIMEOUT_MS, and this is
            // usually called from the UI thread where that would show as a frozen window.
            if (Monitor.TryEnter(streamLock, DISCONNECT_LOCK_WAIT_MS))
            {
                try
                {
                    if (IsLoggedIn && !skipLogout && ConnectionStatus)
                    {
                        // A courtesy to the server. If the socket is already gone this must not escalate into a
                        // broken socket event, because the caller is deliberately closing the connection.
                        try
                        {
                            SendQuery("LOGOUT");
                            ReadResponseLine();
                        }
                        catch (Exception ex)
                        {
                            LogFile.LogTracing("Ignoring error while sending LOGOUT: " + ex.Message, LogLvl.LOG_DEBUG, this);
                        }
                    }

                    CleanupSocket();
                }
                finally
                {
                    Monitor.Exit(streamLock);
                }
            }
            else
            {
                // Someone is mid-query. Closing the socket from under them unblocks their read immediately; they
                // will see it as a broken socket, which is exactly what it now is.
                LogFile.LogTracing("Disconnect could not take the stream lock; closing the socket directly.",
                                   LogLvl.LOG_WARNING, this);
                ForceCloseSocket();
            }
        }

        /// <summary>
        /// Drop the connection without waiting for the stream lock, for when a query is blocked on a read that
        /// will not return until the socket is closed.
        /// </summary>
        private void ForceCloseSocket()
        {
            Interlocked.Increment(ref connectGeneration);
            _isConnected = false;
            _isLoggedIn = false;

            var doomed = client;

            if (doomed != null)
            {
                try
                {
                    doomed.Close();
                }
                catch (Exception ex)
                {
                    LogFile.LogTracing("Ignoring error force-closing TcpClient: " + ex.Message, LogLvl.LOG_DEBUG, this);
                }
            }

            // Closing the socket unblocks whoever was reading, so the lock frees up almost immediately. Take it and
            // finish the teardown properly rather than leaving the streams for the next Connect to clear up.
            if (Monitor.TryEnter(streamLock, DISCONNECT_LOCK_WAIT_MS))
            {
                try
                {
                    CleanupSocket();
                }
                finally
                {
                    Monitor.Exit(streamLock);
                }
            }
            else
            {
                LogFile.LogTracing("Could not finish the teardown under the lock; the next connect will clean up.",
                                   LogLvl.LOG_WARNING, this);
            }
        }

        /// <summary>
        /// Release every socket resource and reset all connection-scoped state. Safe to call at any time, including
        /// when already disconnected, and guaranteed not to throw.
        /// </summary>
        private void CleanupSocket()
        {
            // Any connection attempt still running belongs to a previous generation from here on.
            Interlocked.Increment(ref connectGeneration);

            _isConnected = false;
            _isLoggedIn = false;
            NUTVersion = null;
            NetVersion = null;

            if (WriterStream != null)
            {
                try
                {
                    WriterStream.Dispose();
                }
                catch (Exception ex)
                {
                    LogFile.LogTracing("Ignoring error disposing WriterStream: " + ex.Message, LogLvl.LOG_DEBUG, this);
                }
                WriterStream = null;
            }

            if (ReaderStream != null)
            {
                try
                {
                    ReaderStream.Dispose();
                }
                catch (Exception ex)
                {
                    LogFile.LogTracing("Ignoring error disposing ReaderStream: " + ex.Message, LogLvl.LOG_DEBUG, this);
                }
                ReaderStream = null;
            }

            if (NutStream != null)
            {
                try
                {
                    NutStream.Dispose();
                }
                catch (Exception ex)
                {
                    LogFile.LogTracing("Ignoring error disposing NutStream: " + ex.Message, LogLvl.LOG_DEBUG, this);
                }
                NutStream = null;
            }

            if (client != null)
            {
                try
                {
                    client.Close();
                }
                catch (Exception ex)
                {
                    LogFile.LogTracing("Ignoring error closing TcpClient: " + ex.Message, LogLvl.LOG_DEBUG, this);
                }
                client = null;
            }
        }

        /// <summary>
        /// React to a hard error while using the underlying Socket, and make sure this object is left in a consistent
        /// state. The socket is torn down first so that anything reacting to <see cref="Socket_Broken"/> observes a
        /// cleanly disconnected object.
        /// </summary>
        /// <param name="ex">The exception that broke the socket. Rethrown to the caller when supplied.</param>
        private void OnSocketBroken(Exception ex)
        {
            LogFile.LogTracing("Socket breaking.", LogLvl.LOG_DEBUG, this);

            bool shouldRaise = !socketBrokenRaised;
            socketBrokenRaised = true;

            CleanupSocket();

            if (shouldRaise)
            {
                try
                {
                    Socket_Broken?.Invoke();
                }
                catch (Exception handlerEx)
                {
                    // A failing subscriber must not replace the original exception.
                    LogFile.LogTracing("A Socket_Broken handler threw an exception.", LogLvl.LOG_ERROR, this);
                    LogFile.LogException(handlerEx, this);
                }
            }

            if (ex != null)
            {
                LogFile.LogException(ex, this);
                throw ex;
            }
        }

        /// <summary>
        /// Write a single query line to the server. Caller must hold <see cref="streamLock"/>.
        /// </summary>
        private void SendQuery(string Query_Msg)
        {
            var writer = WriterStream;

            if (writer == null)
            {
                throw new InvalidOperationException("The connection was closed before the query could be sent.");
            }

            writer.WriteLine(Query_Msg);
            writer.Flush();
        }

        /// <summary>
        /// Read a single line of response from the server. Caller must hold <see cref="streamLock"/>.
        /// </summary>
        private string ReadResponseLine()
        {
            var reader = ReaderStream;

            if (reader == null)
            {
                throw new InvalidOperationException("The connection was closed before a response could be read.");
            }

            return reader.ReadLine();
        }

        /// <summary>
        /// Synchronously send a query to the NUT server and collect the response. This method will throw all exceptions,
        /// including NUT protocol (ERR) responses.
        /// </summary>
        /// <param name="Query_Msg">The query to be sent to the server, within specifications of the NUT protocol.</param>
        /// <returns>The full <see cref="Transaction"/> of this function call.</returns>
        /// <exception cref="InvalidOperationException">Thrown when calling this function while disconnected.</exception>
        /// <exception cref="NutException">Thrown when the NUT server returns an error or unexpected response.</exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="IOException">Attempted to read or write to a stream in an invalid state. </exception>
        /// <exception cref="EndOfStreamException">An empty response was encountered, meaning the end of the stream.
        /// This likely indicates that the server closed the connection.</exception>
        public Transaction Query_Data(string Query_Msg)
        {
            string diagnosticQuery = GetDiagnosticQuery(Query_Msg);

            // The lock spans the whole exchange: a query must never be interleaved with another query's response.
            lock (streamLock)
            {
                if (!ConnectionStatus)
                {
                    throw new InvalidOperationException("Attempted to send query " + diagnosticQuery + " while disconnected.");
                }

                try
                {
                    SendQuery(Query_Msg);
                }
                catch (Exception ex)
                {
                    LogFile.LogTracing("Error writing to Stream.", LogLvl.LOG_ERROR, this);
                    OnSocketBroken(ex);
                }

                string response = null;

                try
                {
                    response = ReadResponseLine();
                }
                catch (Exception ex)
                {
                    LogFile.LogTracing("Error reading from Stream.", LogLvl.LOG_ERROR, this);
                    OnSocketBroken(ex);
                }

                return ParseResponse(diagnosticQuery, response);
            }
        }

        /// <summary>
        /// Remove credential material from protocol diagnostics before it can be attached to an exception.
        /// </summary>
        private static string GetDiagnosticQuery(string query)
        {
            if (query != null && query.StartsWith("PASSWORD ", StringComparison.OrdinalIgnoreCase))
            {
                return "PASSWORD [redacted]";
            }

            return query;
        }

        /// <summary>
        /// Turn a raw response line into a <see cref="Transaction"/>, raising the appropriate error for anything that
        /// is not a well-formed success response.
        /// </summary>
        private Transaction ParseResponse(string Query_Msg, string response)
        {
            NUTResponse responseEnum = default;
            string[] splitResponse = null;

            if (string.IsNullOrEmpty(response))
            {
                // End of stream reached, likely server terminated connection.
                OnSocketBroken(new EndOfStreamException("Server terminated connection."));
            }
            else
            {
                splitResponse = response.Split(new[] { ' ' }, 4);

                switch (splitResponse[0])
                {
                    case "OK":
                    case "VAR":
                    case "DESC":
                    case "UPS":
                        responseEnum = NUTResponse.OK;
                        break;
                    case "BEGIN":
                        responseEnum = NUTResponse.BEGINLIST;
                        break;
                    case "END":
                        responseEnum = NUTResponse.ENDLIST;
                        break;
                    case "Network":
                    case "1.0":
                    case "1.1":
                    case "1.2":
                    case "1.3":
                        // In case of "VER" or "NETVER" Query
                        responseEnum = NUTResponse.OK;
                        break;
                    case "ERR":
                        responseEnum = ParseErrorResponse(splitResponse);
                        LogFile.LogTracing($"Parsed error response: { responseEnum }", LogLvl.LOG_DEBUG, this);
                        throw new NutException(new Transaction(Query_Msg, response, responseEnum, splitResponse));
                    default:
                        LogFile.LogTracing($"Unrecognized response while parsing: { response }", LogLvl.LOG_ERROR, this);
                        throw new NutException(new Transaction(Query_Msg, response, NUTResponse.UNRECOGNIZED, splitResponse));
                }
            }

            return new Transaction(Query_Msg, response, responseEnum, splitResponse);
        }

        /// <summary>
        /// Map the textual error name of an ERR response onto <see cref="NUTResponse"/>. An error name this client does
        /// not know about must not crash the parser.
        /// </summary>
        private NUTResponse ParseErrorResponse(string[] splitResponse)
        {
            if (splitResponse.Length < 2)
            {
                return NUTResponse.UNRECOGNIZED;
            }

            try
            {
                return (NUTResponse)Enum.Parse(typeof(NUTResponse),
                                               splitResponse[1].Replace("-", string.Empty));
            }
            catch (Exception)
            {
                LogFile.LogTracing("Unknown NUT error name received: " + splitResponse[1], LogLvl.LOG_WARNING, this);
                return NUTResponse.UNRECOGNIZED;
            }
        }

        public List<UPS_List_Datas> Query_List_Datas(string Query_Msg)
        {
            var List_Datas = new List<string>();
            var List_Result = new List<UPS_List_Datas>();

            // Hold the lock for the whole multi-line exchange. The previous implementation used a hand-managed boolean
            // that was never reset when the loop below threw, which wedged every subsequent query on this object.
            lock (streamLock)
            {
                if (!ConnectionStatus)
                {
                    throw new InvalidOperationException("Attempted to send query " + Query_Msg + " while disconnected.");
                }

                try
                {
                    SendQuery(Query_Msg);
                }
                catch (Exception ex)
                {
                    LogFile.LogTracing("Error writing to Stream.", LogLvl.LOG_ERROR, this);
                    OnSocketBroken(ex);
                }

                string firstLine = null;

                try
                {
                    firstLine = ReadResponseLine();
                }
                catch (Exception ex)
                {
                    LogFile.LogTracing("Error reading from Stream.", LogLvl.LOG_ERROR, this);
                    OnSocketBroken(ex);
                }

                // Raises NutException for an ERR response, and breaks the socket on end-of-stream.
                ParseResponse(Query_Msg, firstLine);

                while (true)
                {
                    string readLine = null;

                    try
                    {
                        readLine = ReadResponseLine();
                    }
                    catch (Exception ex)
                    {
                        LogFile.LogTracing("Error reading LIST response from Stream.", LogLvl.LOG_ERROR, this);
                        OnSocketBroken(ex);
                    }

                    if (readLine == null)
                    {
                        // End of stream in the middle of a list: the server closed the connection on us.
                        OnSocketBroken(new EndOfStreamException("Server terminated connection during LIST response."));
                    }

                    if (readLine.StartsWith("END"))
                    {
                        break;
                    }

                    List_Datas.Add(readLine);
                }
            }

            string Key;
            string Value;
            foreach (var Line in List_Datas)
            {
                var SplitString = Line.Split(new[] { " " }, 4, StringSplitOptions.None);

                switch (SplitString[0])
                {
                    case "BEGIN":
                        break;
                    case "VAR":
                    {
                        // Query
                        // LIST VAR <upsname>
                        // Response List of var
                        // VAR <upsname><varname> "<value>"
                        Key = SplitString[2].Replace("\"", "");
                        Value = SplitString[3].Replace("\"", "");
                        var UPSName = SplitString[1];
                        var VarDESC = GetVarDescriptionOrNothing(Key);
                        List_Result.Add(new UPS_List_Datas
                        {
                            VarKey = Key,
                            VarValue = Value?.Trim(),
                            VarDesc = ExtractDescription(VarDESC)
                        });
                        break;
                    }

                    case "UPS":
                    {
                        // Query
                        // LIST UPS
                        // List of ups
                        // UPS <upsname> "<description>"
                        List_Result.Add(new UPS_List_Datas
                        {
                            VarKey = "UPSNAME",
                            VarValue = SplitString[1],
                            VarDesc = SplitString[2].Replace("\"", "")
                        });
                        break;
                    }
                    case "RW":
                    {
                        // Query
                        // LIST RW <upsname>
                        // List of RW var
                        // RW <upsname><varname> "<value>"
                        Key = SplitString[2].Replace("\"", "");
                        Value = SplitString[3].Replace("\"", "");
                        var UPSName = SplitString[1];
                        var VarDESC = GetVarDescriptionOrNothing(Key);
                        List_Result.Add(new UPS_List_Datas
                        {
                            VarKey = Key,
                            VarValue = Value?.Trim(),
                            VarDesc = ExtractDescription(VarDESC)
                        });
                        break;
                    }
                    case "CMD":
                        // Query
                        // LIST CMD <upsname>
                        // List of CMD
                        // CMD <upsname><cmdname>
                        break;
                    case "ENUM":
                    {
                        // Query
                        // LIST ENUM <upsname>
                        // List of Enum ??
                        // ENUM <upsname><varname> "<value>"
                        Key = SplitString[2].Replace("\"", "");
                        Value = SplitString[3].Replace("\"", "");
                        var UPSName = SplitString[1];
                        var VarDESC = GetVarDescriptionOrNothing(Key);
                        List_Result.Add(new UPS_List_Datas
                        {
                            VarKey = Key,
                            VarValue = Value,
                            VarDesc = ExtractDescription(VarDESC)
                        });
                        break;
                    }
                    case "RANGE":
                        // Query
                        // LIST RANGE <upsname><varname>
                        // List of Range
                        // RANGE <upsname><varname> "<min>" "<max>"
                        break;
                    case "CLIENT":
                        // Query
                        // LIST CLIENT <upsname>
                        // List of Range
                        // CLIENT <device name><client IP address>
                        break;
                }
            }

            return List_Result;
        }

        /// <summary>
        /// Retrieve command names advertised for the UPS. The entire multiline response is read while holding the
        /// same lock as single-line transactions, so it cannot consume a poll's response or be interleaved with it.
        /// </summary>
        public List<string> Query_List_Commands(string upsName)
        {
            string query = "LIST CMD " + upsName;
            var commands = new List<string>();

            lock (streamLock)
            {
                if (!ConnectionStatus)
                {
                    throw new InvalidOperationException("Attempted to list commands while disconnected.");
                }

                try
                {
                    SendQuery(query);
                    string firstLine = ReadResponseLine();
                    if (firstLine == null)
                    {
                        throw new EndOfStreamException("Server terminated connection before the LIST CMD response.");
                    }

                    var begin = ParseResponse(query, firstLine);
                    if (begin.ResponseType != NUTResponse.BEGINLIST || begin.SplitResponse.Length < 4 ||
                        begin.SplitResponse[1] != "LIST" || begin.SplitResponse[2] != "CMD" ||
                        begin.SplitResponse[3] != upsName)
                    {
                        throw new InvalidDataException("The NUT server returned an invalid LIST CMD header.");
                    }

                    while (true)
                    {
                        string line = ReadResponseLine();
                        if (line == null)
                        {
                            throw new EndOfStreamException("Server terminated connection during LIST CMD response.");
                        }

                        if (line == "END LIST CMD " + upsName)
                        {
                            break;
                        }

                        string[] parts = line.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length != 3 || parts[0] != "CMD" || parts[1] != upsName)
                        {
                            throw new InvalidDataException("The NUT server returned an invalid LIST CMD entry.");
                        }

                        commands.Add(parts[2]);
                    }
                }
                catch (NutException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    OnSocketBroken(ex);
                    // OnSocketBroken is expected to throw when given an exception. Keep this explicit so a future
                    // change to that helper cannot let this method return the commands collected so far.
                    throw;
                }
            }

            return commands;
        }

        /// <summary>
        /// Execute one supported UPS instant command on the existing connection. A successful request returns its
        /// transaction; an NUT ERR response follows Query_Data's NutException convention.
        /// </summary>
        public Transaction Execute_Instant_Command(string upsName, string commandName)
        {
            string[] supportedCommands =
            {
                "test.battery.start.quick",
                "test.battery.start.deep",
                "test.battery.stop",
                "beeper.on",
                "beeper.off",
                "beeper.mute",
                "beeper.enable",
                "beeper.disable"
            };

            if (Array.IndexOf(supportedCommands, commandName) < 0)
            {
                throw new ArgumentException("The requested UPS instant command is not supported by NutDesk.", nameof(commandName));
            }

            string query = "INSTCMD " + upsName + " " + commandName;
            Transaction transaction = Query_Data(query);
            bool isPlainSuccess = transaction.SplitResponse != null &&
                                  transaction.SplitResponse.Length == 1 &&
                                  transaction.SplitResponse[0] == "OK";
            bool isTrackedSuccess = transaction.SplitResponse != null &&
                                    transaction.SplitResponse.Length == 3 &&
                                    transaction.SplitResponse[0] == "OK" &&
                                    transaction.SplitResponse[1] == "TRACKING" &&
                                    !string.IsNullOrWhiteSpace(transaction.SplitResponse[2]);
            if (transaction.ResponseType != NUTResponse.OK || (!isPlainSuccess && !isTrackedSuccess))
            {
                throw new InvalidDataException("The NUT server returned an unexpected response to INSTCMD.");
            }

            return transaction;
        }

        /// <summary>
        /// Pull the human readable text out of a `GET DESC` response, tolerating a missing or malformed description.
        /// A variable without a description is not a reason to abandon an entire variable listing.
        /// </summary>
        private string ExtractDescription(string rawDescription)
        {
            if (string.IsNullOrEmpty(rawDescription))
            {
                return string.Empty;
            }

            var parts = rawDescription.Replace("\"", "").Split(new[] { " " }, 4, StringSplitOptions.None);
            return parts.Length >= 4 ? parts[3] : string.Empty;
        }

        /// <summary>
        /// Like <see cref="GetVarDescription(String)"/>, but returns Nothing instead of raising for a variable the
        /// server has no description for. Transport failures are still propagated.
        /// </summary>
        private string GetVarDescriptionOrNothing(string VarName)
        {
            try
            {
                return GetVarDescription(VarName);
            }
            catch (NutException)
            {
                return null;
            }
        }

        public string GetVarDescription(string VarName)
        {
            var Nut_Query = Query_Data("GET DESC " + NutConfig.UPSName + " " + VarName);

            if (Nut_Query.ResponseType == NUTResponse.OK)
            {
                return Nut_Query.RawResponse;
            }
            else
            {
                throw new NutException(Nut_Query);
            }
        }
    }
}
