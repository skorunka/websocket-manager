namespace WebSocketManager
{
	using System;
	using System.Collections.Concurrent;
	using System.Linq;
	using System.Net.WebSockets;
	using System.Threading;
	using System.Threading.Tasks;

	public class WebSocketConnectionManager
	{
		private readonly ConcurrentDictionary<string, WebSocket> _sockets = new ConcurrentDictionary<string, WebSocket>();

		public WebSocket GetSocketById(string id)
		{
			return this._sockets.FirstOrDefault(p => p.Key == id).Value;
		}

		public ConcurrentDictionary<string, WebSocket> GetAll()
		{
			return this._sockets;
		}

		public string GetId(WebSocket socket)
		{
			return this._sockets.FirstOrDefault(p => p.Value == socket).Key;
		}

		public void AddSocket(WebSocket socket)
		{
			this._sockets.TryAdd(this.CreateConnectionId(), socket);
		}

		public async Task RemoveSocket(string id)
		{
			this._sockets.TryRemove(id, out var socket);

			await socket.CloseAsync(WebSocketCloseStatus.NormalClosure,
									"Closed by the WebSocketManager",
									CancellationToken.None).ConfigureAwait(false);
		}

		private string CreateConnectionId()
		{
			return Guid.NewGuid().ToString();
		}
	}
}
