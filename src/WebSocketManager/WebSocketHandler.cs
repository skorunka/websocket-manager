namespace WebSocketManager
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Net.WebSockets;
	using System.Reflection;
	using System.Threading;
	using System.Threading.Tasks;
	using Common;
	using Microsoft.AspNetCore.Http;
	using Newtonsoft.Json;
	using Newtonsoft.Json.Serialization;

	public abstract class WebSocketHandler
	{
		private readonly JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings
		{
			ContractResolver = new CamelCasePropertyNamesContractResolver()
		};

		//TODO: CocurrentDictiopnary
		private readonly Dictionary<string, MethodInfo> _methods = new Dictionary<string, MethodInfo>();

		#region ctors

		protected WebSocketHandler(WebSocketConnectionManager webSocketConnectionManager)
		{
			this.WebSocketConnectionManager = webSocketConnectionManager;
		}

		#endregion

		protected WebSocketConnectionManager WebSocketConnectionManager { get; set; }

		public virtual async Task OnConnected(WebSocket socket, HttpContext context, string socketId = null)
		{
			this.WebSocketConnectionManager.AddSocket(socket, context, socketId);

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

			await socket.SendAsync(message.Serialized, WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
		}

		public async Task SendMessageAsync(string connectionId, Message message)
		{
			await this.SendMessageAsync(this.WebSocketConnectionManager.GetSocketById(connectionId).Socket, message).ConfigureAwait(false);
		}

		public async Task SendMessageToAllAsync(Message message, Func<WebSocketConnection, bool> filter = null)
		{
			var connections = this.WebSocketConnectionManager.Connections();
			if (filter != null)
			{
				connections = connections.Where(filter);
			}

			// Get all connections to send to and send all at once, without blocking
			var allTasks = connections.Select(x => this.SendMessageAsync(x.Socket, message));
			await Task.WhenAll(allTasks).ConfigureAwait(false);
		}

		public async Task InvokeClientMethodAsync(string socketId, string methodName, object[] arguments)
		{
			var message = this.GetInvocationMessage(methodName, arguments);
			await this.SendMessageAsync(socketId, message).ConfigureAwait(false);
		}

		public async Task InvokeClientMethodToAllAsync(string methodName, Func<WebSocketConnection, bool> filter, params object[] arguments)
		{
			var message = this.GetInvocationMessage(methodName, arguments);
			await this.SendMessageToAllAsync(message, filter);
		}

		public async Task InvokeClientMethodToAllAsync(string methodName, params object[] arguments)
		{
			await this.InvokeClientMethodToAllAsync(methodName, null, arguments);
		}

		public async Task ReceiveAsync(WebSocket socket, WebSocketReceiveResult result, string serializedInvocationDescriptor)
		{
			var invocationDescriptor = JsonConvert.DeserializeObject<InvocationDescriptor>(serializedInvocationDescriptor);

			var method = this.GetMethod(invocationDescriptor.MethodName);

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

		private Message GetInvocationMessage(string methodName, object[] arguments)
		{
			return new Message
			{
				MessageType = MessageType.ClientMethodInvocation,
				Data = JsonConvert.SerializeObject(new InvocationDescriptor
				{
					MethodName = methodName,
					Arguments = arguments
				},
				this._jsonSerializerSettings)
			};
		}

		private MethodInfo GetMethod(string methodName)
		{
			if (this._methods.TryGetValue(methodName, out var method))
			{
				return method;
			}

			lock (this)
			{
				if (this._methods.TryGetValue(methodName, out method))
				{
					return method;
				}

				// Todo: make sure this method can be invoked
				method = this.GetType().GetMethod(methodName);
				if ((method != null) && !this._methods.ContainsKey(methodName))
				{
					this._methods.Add(methodName, method);
				}

				return method;
			}
		}
	}
}
