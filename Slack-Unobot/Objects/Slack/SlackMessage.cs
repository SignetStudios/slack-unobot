using Newtonsoft.Json;
using System.Collections.Generic;

namespace SlackUnobot.Objects.Slack
{
	public class SlackMessage
	{
		public string Token { get; set; }
		public string Channel { get; set; }
		public string Text { get; set; }
		[JsonProperty("as_user")]
		public bool AsUser { get; set; }
		public List<Attachment> Attachments { get; set; }
		[JsonProperty("icon_emoji")]
		public string IconEmoji { get; set; }
		[JsonProperty("icon_url")]
		public string IconUrl { get; set; }
		[JsonProperty("link_names")]
		public bool LinkNames { get; set; }
		public string Parse { get; set; }
		[JsonProperty("reply_broadcast")]
		public bool ReplyBroadcast { get; set; }
		[JsonProperty("thread_ts")]
		public string ThreadTs { get; set; }
		[JsonProperty("unfurl_links")]
		public bool UnfurlLinks { get; set; }
		[JsonProperty("unfurl_media")]
		public bool UnfurlMedia { get; set; }
		public string Username { get; set; }
		[JsonProperty("bot_id")]
		public string BotId { get; set; }
		public string Type { get; set; }
		public string Subtype { get; set; }
		public string Ts { get; set; }
		public string Error { get; set; }
		public string User { get; set; }
		[JsonProperty("delete_original")]
		public bool DeleteOriginal { get; set; }
	}
}
