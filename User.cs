using System;
using System.Web.WebSockets;

namespace SimpleWebSocketApplication
{
    class User
    {
        public string Name = String.Empty;
        public AspNetWebSocketContext Context { get; set; }
    }
}