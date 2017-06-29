namespace WebSocketManager
{
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.Linq;
	using System.Net.WebSockets;
	using System.Threading;
	using System.Threading.Tasks;
	using Microsoft.AspNetCore.Http;

	public class WebSocketConnectionManager
	{
		private readonly ConcurrentDictionary<string, WebSocketConnection> _sockets = new ConcurrentDictionary<string, WebSocketConnection>();

		public WebSocketConnection GetSocketById(string id)
		{
			return this._sockets.FirstOrDefault(p => p.Key == id).Value;
		}

		public ConcurrentDictionary<string, WebSocketConnection> GetAll()
		{
			return this._sockets;
		}

		public IEnumerable<WebSocketConnection> Connections()
		{
			return this._sockets.Values.Where(x => x.Socket.State == WebSocketState.Open);
		}

		public string GetId(WebSocket socket)
		{
			return this._sockets.Values.FirstOrDefault(p => p.Socket == socket)?.Id;
		}

		public void AddSocket(WebSocket socket, HttpContext context, string socketId = null)
		{
			var id = socketId ?? CreateConnectionId();
			this._sockets.TryAdd(id, new WebSocketConnection { Id = id, Socket = socket, Query = context.Request.Query });
		}

		public async Task RemoveSocket(string id)
		{
			if (this._sockets.TryRemove(id, out var connection))
			{
				await connection.Socket.CloseAsync(WebSocketCloseStatus.NormalClosure,
													"Closed by the WebSocketManager",
													CancellationToken.None).ConfigureAwait(false);
			}
		}

		private static string CreateConnectionId()
		{
			return Guid.NewGuid().ToString();
		}
	}
}
