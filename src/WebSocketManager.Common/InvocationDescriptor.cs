namespace WebSocketManager.Common
{
	using Newtonsoft.Json;

	public class InvocationDescriptor
	{
		[JsonProperty("methodName")]
		public string MethodName { get; set; }

		[JsonProperty("arguments")]
		public object[] Arguments { get; set; }
	}
}
