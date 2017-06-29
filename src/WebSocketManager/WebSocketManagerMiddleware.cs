namespace WebSocketManager
{
	using System;
	using System.IO;
	using System.Net.WebSockets;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using Microsoft.AspNetCore.Http;
	using Microsoft.Extensions.Logging;

	public class WebSocketManagerMiddleware
	{
		private readonly ILogger<WebSocketManagerMiddleware> _logger;

		#region ctors

		public WebSocketManagerMiddleware(RequestDelegate next, WebSocketHandler webSocketHandler, ILogger<WebSocketManagerMiddleware> logger)
		{
			this._webSocketHandler = webSocketHandler;
			this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
		}

		#endregion

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
									catch (WebSocketException e)
									{
										this._logger.LogError(e.Message, e);
										throw;
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
