namespace WebSocketManager
{
	using System.Net.WebSockets;
	using Microsoft.AspNetCore.Http;

	public class WebSocketConnection
	{
		public string Id;

		public WebSocket Socket;

		public IQueryCollection Query { get; internal set; }
	}
}
