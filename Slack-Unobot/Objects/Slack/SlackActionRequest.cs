using System.Collections.Generic;
using Newtonsoft.Json;

namespace SlackUnobot.Objects.Slack
{
	public class SlackActionRequest
	{
		[JsonProperty("response_url")]
		public string ResponseUrl { get; set; }

		[JsonProperty("trigger_id")]
		public string TriggerId { get; set; }
		public string Token { get; set; }
		public List<Action> Actions { get; set; }

		[JsonProperty("callback_id")]
		public string CallbackId { get; set; }

		public Team Team { get; set; }

		public IdName Channel { get; set; }

		public IdName User { get; set; }

		[JsonProperty("action_ts")]
		public string ActionTs { get; set; }

		[JsonProperty("message_ts")]
		public string MessageTs { get; set; }

		[JsonProperty("attachment_id")]
		public string AttachmentId { get; set; }
	}
}