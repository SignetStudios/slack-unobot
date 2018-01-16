using Newtonsoft.Json;
using System.Collections.Generic;

namespace SlackUnobot.Objects.Slack
{
	public class Attachment
	{
		public string Color { get; set; }
		public string Text { get; set; }
		public string Pretext { get; set; }
		public List<Action> Actions { get; set; }
		[JsonProperty("callback_id")]
		public string CallbackId { get; set; }
		public string Fallback { get; set; }
	}
}