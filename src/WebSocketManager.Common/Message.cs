namespace WebSocketManager.Common
{
	using System;
	using System.Text;
	using Newtonsoft.Json;
	using Newtonsoft.Json.Serialization;

	public class Message
	{
		private static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings
		{
			ContractResolver = new CamelCasePropertyNamesContractResolver()
		};

		private ArraySegment<byte>? serialized;

		public MessageType MessageType { get; set; }

		public string Data { get; set; }

		[JsonIgnore]
		public ArraySegment<byte> Serialized
		{
			get
			{
				if (this.serialized.HasValue)
				{
					return this.serialized.Value;
				}

				lock (this)
				{
					if (this.serialized.HasValue)
					{
						return this.serialized.Value;
					}

					var serializedMessage = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(this, JsonSerializerSettings));
					this.serialized = new ArraySegment<byte>(serializedMessage, 0, serializedMessage.Length);
				}

				return this.serialized.Value;
			}
		}
	}
}
