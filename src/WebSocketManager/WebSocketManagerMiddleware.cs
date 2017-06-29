namespace WebSocketManager
{
	using System;
	using System.IO;
	using System.Net.WebSockets;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using Microsoft.AspNetCore.Http;

	public class WebSocketManagerMiddleware
	{
		public WebSocketManagerMiddleware(RequestDelegate next, WebSocketHandler webSocketHandler)
		{
			this._webSocketHandler = webSocketHandler;
		}

		private WebSocketHandler _webSocketHandler { get; }

		public virtual async Task Invoke(HttpContext context)
		{
			if (!context.WebSockets.IsWebSocketRequest)
			{
				context.Response.StatusCode = 400;
				return;
			}

			var socket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);

			await this._webSocketHandler.OnConnected(socket, context).ConfigureAwait(false);

			await Receive(socket,
						async (result, serializedInvocationDescriptor) =>
						{
							switch (result.MessageType)
							{
								case WebSocketMessageType.Text:
									await this._webSocketHandler.ReceiveAsync(socket, result, serializedInvocationDescriptor).ConfigureAwait(false);
									break;
								case WebSocketMessageType.Close:
									try
									{
										await this._webSocketHandler.OnDisconnected(socket);
									}
									catch (WebSocketException)
									{
										throw; //let's not swallow any exception for now
									}
									break;
								default:
									throw new ArgumentOutOfRangeException($"Unsupported WebSocket type: {result.MessageType}");
							}
						});
		}

		private static async Task Receive(WebSocket socket, Action<WebSocketReceiveResult, string> handleMessage)
		{
			var buffer = new ArraySegment<byte>(new byte[1024 * 4]);
			using (var ms = new MemoryStream())
			{
				while (socket.State == WebSocketState.Open)
				{
					WebSocketReceiveResult result;
					do
					{
						result = await socket.ReceiveAsync(buffer, CancellationToken.None).ConfigureAwait(false);
						ms.Write(buffer.Array, buffer.Offset, result.Count);
					} while (!result.EndOfMessage);

					var serializedInvocationDescriptor = Encoding.UTF8.GetString(ms.ToArray());
					ms.SetLength(0);

					handleMessage(result, serializedInvocationDescriptor);
				}
			}
		}
	}
}
