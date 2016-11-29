using System;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.WebSockets;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace SimpleWebSocketApplication
{
    public class WebSocketHandler : IHttpHandler
    {
        private static ConcurrentDictionary<User, string> onlineUsers = new ConcurrentDictionary<User, string>();
        
        

        public void ProcessRequest(HttpContext context)
        {
            //Checks if the query is WebSocket request. 
            if (context.IsWebSocketRequest)
            {
                //If yes, we attach the asynchronous handler.
                context.AcceptWebSocketRequest(WebSocketRequestHandler);
            }
        }

        public bool IsReusable { get { return false; } }

        internal static ConcurrentDictionary<User, string> OnlineUsers
        {
            get
            {
                return onlineUsers;
            }

            set
            {
                onlineUsers = value;
            }
        }
        
        //Asynchronous request handler.
        public async Task WebSocketRequestHandler(AspNetWebSocketContext webSocketContext)
        {
            var me = new User { Name = webSocketContext.Timestamp.ToString(), Context = webSocketContext };
            OnlineUsers.TryAdd(me, String.Empty);

            AspNetWebSocketContext wsc = webSocketContext;
            //Gets the current WebSocket object.
            WebSocket webSocket = wsc.WebSocket;

            /*We define a certain constant which will represent
            size of received data. It is established by us and 
            we can set any value. We know that in this case the size of the sent
            data is very small.
            */
            const int maxMessageSize = 1024;

            //Buffer for received bits.
            var receivedDataBuffer = new ArraySegment<Byte>(new Byte[maxMessageSize]);

            var cancellationToken = new CancellationToken();

            //Checks WebSocket state.
            while (webSocket.State == WebSocketState.Open)
            {
                //Reads data.
                WebSocketReceiveResult webSocketReceiveResult =
                  await webSocket.ReceiveAsync(receivedDataBuffer, cancellationToken);

                //If input frame is cancelation frame, send close command.
                if (webSocketReceiveResult.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure,
                      String.Empty, cancellationToken);
                }
                else
                {
                    byte[] payloadData = receivedDataBuffer.Array.Where(b => b != 0).ToArray();

                    //Because we know that is a string, we convert it.
                    string receiveString =
                      System.Text.Encoding.UTF8.GetString(payloadData, 0, payloadData.Length);

                    //Gelen mesajı JSONa seriliaze edip kimliğine bakılacak, eğer gonderen doğrulanır ise aldığımız mesajı sendMSg ile gonderebilriz.

                    try
                    {
                        var receivedMSG = JsonConvert.DeserializeObject<Mesaj>(receiveString);
                        if (receivedMSG.ID == 1)
                        {
                            var newMsg = new Mesaj();
                            newMsg.ID = 1001;
                            newMsg.Sicaklik = receivedMSG.Sicaklik;
                            await Broadcast(JsonConvert.SerializeObject(newMsg));
                        }
                        else
                        {
                            await SendMsg("Wrong JSON ...", webSocketContext);
                        }
                    }
                    catch (Exception ex)
                    {
                        await SendMsg(ex.ToString(), webSocketContext);
                    }

                    /*
                    //Converts string to byte array.
                    var newString =
                      String.Format("Hello, " + receiveString + " ! Time {0}", DateTime.Now.ToString());
                    Byte[] bytes = System.Text.Encoding.UTF8.GetBytes(newString);
                    
                    //Sends data back.
                    await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);

                    */
                }
            }
        }

        public async Task SendMsg(string msg, AspNetWebSocketContext C)
        {
            var cancellationToken = new CancellationToken();
            Byte[] bytes = System.Text.Encoding.UTF8.GetBytes(msg);
            await C.WebSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
            
        }

        private async Task Broadcast(string message, ICollection<User> users = null)
        {
            Byte[] bytes = System.Text.Encoding.UTF8.GetBytes(message);

            var cancellationToken = new CancellationToken();

            if (users == null)
            {
                foreach (var user in OnlineUsers.Keys)
                {
                    if (user.Context.IsClientConnected)
                    await user.Context.WebSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
                }
            }
            else
            {
                foreach (var user in OnlineUsers.Keys.Where(users.Contains))
                {
                    if (user.Context.IsClientConnected)
                        await user.Context.WebSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
                }
            }
        }

    }
}