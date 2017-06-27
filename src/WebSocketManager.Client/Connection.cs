namespace WebSocketManager.Client
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Net.WebSockets;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using Common;
	using Newtonsoft.Json;
	using Newtonsoft.Json.Serialization;

	public class Connection
	{
		private readonly Dictionary<string, InvocationHandler> _handlers = new Dictionary<string, InvocationHandler>();

		private readonly JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings
		{
			ContractResolver = new CamelCasePropertyNamesContractResolver()
		};

		public Connection()
		{
			this._clientWebSocket = new ClientWebSocket();
		}

		public string ConnectionId { get; set; }

		private ClientWebSocket _clientWebSocket { get; }

		public async Task StartConnectionAsync(string uri)
		{
			await this._clientWebSocket.ConnectAsync(new Uri(uri), CancellationToken.None).ConfigureAwait(false);

			await this.Receive(this._clientWebSocket,
								message =>
								{
									if (message.MessageType == MessageType.ConnectionEvent)
									{
										this.ConnectionId = message.Data;
									}

									else if (message.MessageType == MessageType.ClientMethodInvocation)
									{
										var invocationDescriptor = JsonConvert.DeserializeObject<InvocationDescriptor>(message.Data, this._jsonSerializerSettings);
										this.Invoke(invocationDescriptor);
									}
								});
		}

		public void On(string methodName, Action<object[]> handler)
		{
			var invocationHandler = new InvocationHandler(handler, new Type[] { });
			this._handlers.Add(methodName, invocationHandler);
		}

		private void Invoke(InvocationDescriptor invocationDescriptor)
		{
			var invocationHandler = this._handlers[invocationDescriptor.MethodName];
			if (invocationHandler != null)
			{
				invocationHandler.Handler(invocationDescriptor.Arguments);
			}
		}

		public async Task StopConnectionAsync()
		{
			await this._clientWebSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).ConfigureAwait(false);
		}

		private async Task Receive(ClientWebSocket clientWebSocket, Action<Message> handleMessage)
		{
			while (this._clientWebSocket.State == WebSocketState.Open)
			{
				var buffer = new ArraySegment<byte>(new byte[1024 * 4]);
				string serializedMessage = null;
				WebSocketReceiveResult result = null;
				using (var ms = new MemoryStream())
				{
					do
					{
						result = await clientWebSocket.ReceiveAsync(buffer, CancellationToken.None).ConfigureAwait(false);
						ms.Write(buffer.Array, buffer.Offset, result.Count);
					} while (!result.EndOfMessage);

					ms.Seek(0, SeekOrigin.Begin);

					using (var reader = new StreamReader(ms, Encoding.UTF8))
					{
						serializedMessage = await reader.ReadToEndAsync().ConfigureAwait(false);
					}
				}

				if (result.MessageType == WebSocketMessageType.Text)
				{
					var message = JsonConvert.DeserializeObject<Message>(serializedMessage);
					handleMessage(message);
				}

				else if (result.MessageType == WebSocketMessageType.Close)
				{
					await this._clientWebSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).ConfigureAwait(false);
					break;
				}
			}
		}
	}

	public class InvocationHandler
	{
		public InvocationHandler(Action<object[]> handler, Type[] parameterTypes)
		{
			this.Handler = handler;
			this.ParameterTypes = parameterTypes;
		}

		public Action<object[]> Handler { get; set; }
		public Type[] ParameterTypes { get; set; }
	}
}
