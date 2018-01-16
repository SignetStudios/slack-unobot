using Newtonsoft.Json;
using System.Collections.Generic;

namespace SlackUnobot.Objects.Slack
{
	public class SlackRequest
	{
		[JsonProperty("response_url")]
		public string ResponseUrl { get; set; }

		[JsonProperty("trigger_id")]
		public string TriggerId { get; set; }
		public string Token { get; set; }
		[JsonProperty("team_id")]
		public string TeamId { get; set; }

		[JsonProperty("team_domain")]
		public string TeamDomain { get; set; }

		[JsonProperty("channel_id")]
		public string ChannelId { get; set; }

		[JsonProperty("channel_name")]
		public string ChannelName { get; set; }

		[JsonProperty("user_id")]
		public string UserId { get; set; }

		[JsonProperty("user_name")]

		public string UserName { get; set; }

		public string Command { get; set; }

		public string Text { get; set; }
	}
	
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

	public class Action
	{
		public string Name { get; set; }
		public string Value { get; set; }
		public string Type { get; set; }
		public string Text { get; set; }
	}

	public class Team
	{
		public string Id { get; set; }
		public string Domain { get; set; }
	}

	public class IdName
	{
		public string Id { get; set; }
		public string Name { get; set; }
	}

	public static partial class Extensions
	{
		public static SlackRequest ToSlackRequest(this SlackActionRequest action)
		{
			return new SlackRequest
			       {
				       ResponseUrl = action.ResponseUrl,
				       Token = action.Token,
				       TriggerId = action.TriggerId,
				       ChannelId = action.Channel.Id,
				       ChannelName = action.Channel.Name,
				       TeamDomain = action.Team.Domain,
				       TeamId = action.Team.Id,
				       UserId = action.User.Id,
				       UserName = action.User.Name
			       };
		}

		public static SlackActionRequest ToActionRequest(this SlackRequest request)
		{
			return new SlackActionRequest
			       {
				       ResponseUrl = request.ResponseUrl,
				       Token = request.Token,
				       TriggerId = request.TriggerId,
				       Channel = new IdName
				                 {
					                 Id = request.ChannelId,
					                 Name = request.ChannelName
				                 },
				       User = new IdName
				              {
					              Id = request.UserId,
					              Name = request.UserName
				              },
				       Team = new Team
				              {
					              Id = request.TeamId,
					              Domain = request.TeamDomain
				              }
			       };
		}
	}
}