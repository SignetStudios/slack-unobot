namespace SlackUnobot.Objects.Slack
{
	public class SlackResponse
	{
		public bool Ok { get; set; }
		public string Channel { get; set; }
		public string Ts { get; set; }
		public SlackMessage Message { get; set; }
	}
}