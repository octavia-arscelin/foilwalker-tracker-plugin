using FoilwalkerTracker.Windows;
using FoilwalkerTrackerLib.Model;
using FoilwalkerTrackerLib.Networking;
using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace FoilwalkerTracker
{
    public enum ConnectionStatus
    {
        OFFLINE, CONNECTING, SOCKET_ESTABLISHED, SOCKET_FAILURE, LOGIN_SUCCESS, LOGIN_FAILURE, CONNECTION_LOST, AUTHENTICATION_FAILURE
    }

    internal class ClientNetworkingHandler(Plugin plugin) : IDisposable
    {
        private Plugin plugin = plugin;

        public event EventHandler<ConnectionStatus> OnConnectionUpdate;
        public event EventHandler<FWTGameWrapperSerializable[]> OnGameListReceived;
        public event EventHandler<GameJoinResponseEventArgs> OnGameJoin;
        public event EventHandler<FWTGame> OnGameUpdate;
        public event EventHandler<FWTActionRequest> OnActionRequestReceived;
        public event EventHandler<FWTActionAcknowledge> OnActionAcknowledgeReceived;
        public event EventHandler<FWTGameLeaveResponse> OnGameLeaveResponseReceived;

        private TcpClient? client;
        private SslStream? stream;

        public async Task<int> ConnectToServerAsync(string host, int port)
        {
                client = new TcpClient();
                await client.ConnectAsync(host, port);
                stream = new SslStream(client.GetStream(), false, new RemoteCertificateValidationCallback(ValidateServerCertificate), null);
                try
                {
                    await stream.AuthenticateAsClientAsync(host,null,SslProtocols.Tls12 | SslProtocols.Tls13,true);
                    OnConnectionUpdate.Invoke(this, ConnectionStatus.SOCKET_ESTABLISHED);
                    Task.Run(() => receiveMessagesAsync());
                    return 0;
                }
                catch(AuthenticationException ex)
                {
                    OnConnectionUpdate.Invoke(this, ConnectionStatus.AUTHENTICATION_FAILURE);
                    return -5;
                }
                catch(Exception ex)
                {
                    OnConnectionUpdate.Invoke(this,ConnectionStatus.SOCKET_FAILURE);
                    plugin.outputToLog(ex.Message);
                    return -1;
                }
        }

        private bool ValidateServerCertificate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None) return true;
            plugin.outputToLog($"Certificate error: {sslPolicyErrors}");
            return false;
        }

        public int SendMessage<T>(T message) where T : FWTMessage
        {
            //MemoryStream memoryStream = new MemoryStream();
            if (stream == null) return -1;
            XmlSerializer serializer = new XmlSerializer(typeof(T));
            serializer.Serialize(stream, message);
            //serializer.Serialize(memoryStream, message);
            //plugin.outputToLog(Encoding.ASCII.GetString(memoryStream.ToArray()));
            return 0;
        }

        public async Task receiveMessagesAsync()
        {
            XmlSerializer serializer = new XmlSerializer(typeof(FWTMessage));
            while (true)
            {
                try
                {
                    MemoryStream memoryStream = new MemoryStream();
                    var bytes = new byte[2048*32];
                    var i = 0;
                    var off = stream.Read(bytes, 0, bytes.Length);
                    memoryStream.Write(bytes, 0, off);
                    if (memoryStream.Length == 0) break;
                    memoryStream.Seek(0, SeekOrigin.Begin);
                    var message = serializer.Deserialize(memoryStream) as FWTMessage;
                    memoryStream.Seek(0, SeekOrigin.Begin);
                    switch (message?.operation)
                    {
                        case MessageType.LOGIN_RESPONSE:
                            {
                                var inSerializer = new XmlSerializer(typeof(FWTLoginResponse));
                                if (inSerializer.Deserialize(memoryStream) is FWTLoginResponse response) HandleLoginResponse(response);
                            }
                            break;
                        case MessageType.GAMELIST_RESPONSE:
                            {
                                var inSerializer = new XmlSerializer(typeof(FWTGameListResponse));
                                if (inSerializer.Deserialize(memoryStream) is FWTGameListResponse response) HandleGameListResponse(response);
                            }
                            break;
                        case MessageType.GAMEJOIN_RESPONSE:
                            {
                                var inSerializer = new XmlSerializer(typeof(FWTGameJoinResponse));
                                if (inSerializer.Deserialize(memoryStream) is FWTGameJoinResponse response) HandleGameJoinResponse(response);
                            }
                            break;
                        case MessageType.GAME_UPDATE:
                            {
                                var inSerializer = new XmlSerializer(typeof(FWTGameUpdate));
                                if (inSerializer.Deserialize(memoryStream) is FWTGameUpdate response) HandleGameUpdate(response);
                            }
                            break;
                        case MessageType.ACTION_REQUEST:
                            {
                                var inSerializer = new XmlSerializer(typeof(FWTActionRequest));
                                if (inSerializer.Deserialize(memoryStream) is FWTActionRequest response) HandleActionRequest(response);
                            }
                            break;
                        case MessageType.ACTION_ACKNOWLEDGE:
                            {
                                var inSerializer = new XmlSerializer(typeof(FWTActionAcknowledge));
                                if (inSerializer.Deserialize(memoryStream) is FWTActionAcknowledge response) HandleActionAcknowledge(response);
                            }
                            break;
                        case MessageType.GAMELEAVE_RESPONSE:
                            {
                                var inSerializer = new XmlSerializer(typeof(FWTGameLeaveResponse));
                                if (inSerializer.Deserialize(memoryStream) is FWTGameLeaveResponse response) HandleGameLeaveResponse(response);
                            }
                            break;
                        default:
                            Console.WriteLine("Unknown message type; discarded");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    plugin.outputToLog(ex.Message);
                    plugin.outputToLog(ex.StackTrace);
                    if (ex.InnerException != null) 
                    { 
                        plugin.outputToLog(ex.InnerException.Message);
                        plugin.outputToLog(ex.InnerException.StackTrace);
                    }
                    plugin.outputToLog("Unhandled exception : disconnecting");
                    if (stream != null) stream.Dispose();
                    if (client != null) client.Dispose();
                    OnConnectionUpdate(this, ConnectionStatus.CONNECTION_LOST);
                    break;
                }
            }
            OnConnectionUpdate.Invoke(this, ConnectionStatus.OFFLINE);
        }

        private void HandleGameLeaveResponse(FWTGameLeaveResponse response)
        {
            if(response.gameId == plugin.gameId)
            {
                OnGameLeaveResponseReceived.Invoke(this, response);
            }
        }

        private void HandleActionAcknowledge(FWTActionAcknowledge response)
        {
            if(response.gameId == plugin.gameId)
            {
                OnActionAcknowledgeReceived.Invoke(this, response);
            }
        }

        private void HandleActionRequest(FWTActionRequest response)
        {
            if(response.gameId == plugin.gameId)
            {
                OnActionRequestReceived.Invoke(this, response);
            }
        }

        private void HandleGameUpdate(FWTGameUpdate response)
        {
            if(response.game != null)
            {
                OnGameUpdate.Invoke(this, response.game);
            }
        }

        private void HandleGameJoinResponse(FWTGameJoinResponse response)
        {
            if (response.success && response.game != null)
            {
                OnGameJoin.Invoke(this,new GameJoinResponseEventArgs(response.game,response.characterId,response.gameId,response.admin));   
            }
        }

        private void HandleLoginResponse(FWTLoginResponse response)
        {
            if (response.success == 0) { OnConnectionUpdate.Invoke(this, ConnectionStatus.LOGIN_SUCCESS); SendMessage(new FWTGameListRequest()); }
            else if (response.success == 2) { OnConnectionUpdate.Invoke(this, ConnectionStatus.LOGIN_SUCCESS); SendMessage(new FWTGameListRequest()); }
            else { OnConnectionUpdate.Invoke(this, ConnectionStatus.LOGIN_FAILURE); }
        }

        private void HandleGameListResponse(FWTGameListResponse response)
        {
            OnGameListReceived.Invoke(this,response.wrappers);
        }

        public void Dispose()
        {
            if(client != null)((IDisposable)client).Dispose();
            if(stream != null)stream.Dispose();
        }

        internal void OnLocalCharacterUpdate(object? sender, FWTCharacter e)
        {
            SendMessage(new FWTLocalCharacterUpdate(plugin.gameId,e));
        }

        internal void OnMobCreated(object? sender, FWTMob e)
        {
            SendMessage(new FWTMobCreateRequest(plugin.gameId,e));
        }

        internal void OnMobRemoved(object? sender, FWTMob e)
        {
            SendMessage(new FWTMobRemoveRequest(plugin.gameId, e));
        }

        internal void OnActionRequest(object? sender, FWTActionParameters e)
        {
            if (e.target == null) return;
            SendMessage(new FWTActionRequest(plugin.gameId, -1, e,plugin.characterId));
        }

        internal void OnRequestAcknowledged(object? sender, long e)
        {
            SendMessage(new FWTActionAcknowledge(plugin.gameId, e, true));
        }

        internal void OnRequestDenied(object? sender, long e)
        {
            SendMessage(new FWTActionAcknowledge(plugin.gameId, e, false));
        }

        internal void Disconnect()
        {
            SendMessage(new FWTLogoffRequest());
        }

        internal void OnGameCreateRequest(object? sender, GameListWindow.GameCreateRequestEventArgs e)
        {
            if (plugin.connectionStatus == ConnectionStatus.LOGIN_SUCCESS && plugin.currentGame == null)
            {
                SendMessage(new FWTGameCreateRequest(e.gameName, e.character));
            }
        }

        internal void OnGameJoinRequest(object? sender, GameListWindow.GameJoinRequestEventArgs e)
        {
            if (plugin.connectionStatus == ConnectionStatus.LOGIN_SUCCESS && plugin.currentGame == null)
            {
                SendMessage(new FWTGameJoinRequest(e.game.id, e.character));
            }
        }

        internal void OnGameLeaveRequest(object? sender, long e)
        {
            if (plugin.connectionStatus == ConnectionStatus.LOGIN_SUCCESS && plugin.currentGame != null)
            {
                SendMessage(new FWTGameLeaveRequest(e));
            }
        }

        internal class GameJoinResponseEventArgs(FWTGame game, long characterId, long gameId, bool admin)
        {
            public FWTGame game = game;
            public long characterId = characterId;
            public long gameId = gameId;
            public bool admin = admin;
        }
    }
}
