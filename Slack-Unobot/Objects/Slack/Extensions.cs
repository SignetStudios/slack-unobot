namespace SlackUnobot.Objects.Slack
{
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