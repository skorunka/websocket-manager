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
			this._sockets.TryGetValue(id, out var ws);
			return ws;
		}

		public IEnumerable<WebSocketConnection> Connections()
		{
			return this._sockets.Values.Where(x => x.Socket.State == WebSocketState.Open);
		}

		public string GetId(WebSocket socket)
		{
			return this._sockets.Values.FirstOrDefault(p => p.Socket == socket)?.Id;
		}

		public void AddSocket(WebSocket socket, HttpContext context, string socketConnectionId = null)
		{
			var id = socketConnectionId ?? CreateSocketConnectionId();
			this._sockets.TryAdd(id, new WebSocketConnection { Id = id, Socket = socket, Query = context.Request.Query });
		}

		public async Task RemoveSocket(string id)
		{
			if (this._sockets.TryRemove(id, out var socketConnection))
			{
				await socketConnection.Socket
					.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by the WebSocketManager", CancellationToken.None)
					.ConfigureAwait(false);
			}
		}

		private static string CreateSocketConnectionId()
		{
			return Guid.NewGuid().ToString();
		}
	}
}
