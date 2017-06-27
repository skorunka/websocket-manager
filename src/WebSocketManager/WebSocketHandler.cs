namespace WebSocketManager
{
	using System;
	using System.Net.WebSockets;
	using System.Reflection;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using Common;
	using Newtonsoft.Json;
	using Newtonsoft.Json.Serialization;

	public abstract class WebSocketHandler
	{
		private readonly JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings
		{
			ContractResolver = new CamelCasePropertyNamesContractResolver()
		};

		public WebSocketHandler(WebSocketConnectionManager webSocketConnectionManager)
		{
			this.WebSocketConnectionManager = webSocketConnectionManager;
		}

		protected WebSocketConnectionManager WebSocketConnectionManager { get; set; }

		public virtual async Task OnConnected(WebSocket socket)
		{
			this.WebSocketConnectionManager.AddSocket(socket);

			await this.SendMessageAsync(socket,
										new Message
										{
											MessageType = MessageType.ConnectionEvent,
											Data = this.WebSocketConnectionManager.GetId(socket)
										}).ConfigureAwait(false);
		}

		public virtual async Task OnDisconnected(WebSocket socket)
		{
			await this.WebSocketConnectionManager.RemoveSocket(this.WebSocketConnectionManager.GetId(socket)).ConfigureAwait(false);
		}

		public async Task SendMessageAsync(WebSocket socket, Message message)
		{
			if (socket.State != WebSocketState.Open)
			{
				return;
			}

			var serializedMessage = JsonConvert.SerializeObject(message, this._jsonSerializerSettings);
			await socket.SendAsync(new ArraySegment<byte>(Encoding.ASCII.GetBytes(serializedMessage),
														0,
														serializedMessage.Length),
									WebSocketMessageType.Text,
									true,
									CancellationToken.None).ConfigureAwait(false);
		}

		public async Task SendMessageAsync(string socketId, Message message)
		{
			await this.SendMessageAsync(this.WebSocketConnectionManager.GetSocketById(socketId), message).ConfigureAwait(false);
		}

		public async Task SendMessageToAllAsync(Message message)
		{
			foreach (var pair in this.WebSocketConnectionManager.GetAll())
			{
				if (pair.Value.State == WebSocketState.Open)
				{
					await this.SendMessageAsync(pair.Value, message).ConfigureAwait(false);
				}
			}
		}

		public async Task InvokeClientMethodAsync(string socketId, string methodName, object[] arguments)
		{
			var message = new Message
			{
				MessageType = MessageType.ClientMethodInvocation,
				Data = JsonConvert.SerializeObject(new InvocationDescriptor
													{
														MethodName = methodName,
														Arguments = arguments
													},
													this._jsonSerializerSettings)
			};

			await this.SendMessageAsync(socketId, message).ConfigureAwait(false);
		}

		public async Task InvokeClientMethodToAllAsync(string methodName, params object[] arguments)
		{
			foreach (var pair in this.WebSocketConnectionManager.GetAll())
			{
				if (pair.Value.State == WebSocketState.Open)
				{
					await this.InvokeClientMethodAsync(pair.Key, methodName, arguments).ConfigureAwait(false);
				}
			}
		}

		public async Task ReceiveAsync(WebSocket socket, WebSocketReceiveResult result, string serializedInvocationDescriptor)
		{
			var invocationDescriptor = JsonConvert.DeserializeObject<InvocationDescriptor>(serializedInvocationDescriptor);

			var method = this.GetType().GetMethod(invocationDescriptor.MethodName);

			if (method == null)
			{
				await this.SendMessageAsync(socket,
											new Message
											{
												MessageType = MessageType.Text,
												Data = $"Cannot find method {invocationDescriptor.MethodName}"
											}).ConfigureAwait(false);
				return;
			}

			try
			{
				method.Invoke(this, invocationDescriptor.Arguments);
			}
			catch (TargetParameterCountException)
			{
				await this.SendMessageAsync(socket,
											new Message
											{
												MessageType = MessageType.Text,
												Data = $"The {invocationDescriptor.MethodName} method does not take {invocationDescriptor.Arguments.Length} parameters!"
											}).ConfigureAwait(false);
			}

			catch (ArgumentException)
			{
				await this.SendMessageAsync(socket,
											new Message
											{
												MessageType = MessageType.Text,
												Data = $"The {invocationDescriptor.MethodName} method takes different arguments!"
											}).ConfigureAwait(false);
			}
		}
	}
}
