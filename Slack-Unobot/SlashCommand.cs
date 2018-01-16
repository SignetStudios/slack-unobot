using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Script.Serialization;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using SlackUnobot.Objects.Slack;
using SlackUnobot.Services;
#pragma warning disable 4014

namespace SlackUnobot
{
	public static class SlashCommand
	{
		private static readonly Regex Play = new Regex(
			@"^play(?: (?<color>r(?:ed)?|y(?:ellow)?|g(?:reen)?|b(?:lue)?|w(?:ild)?|d(?:raw ?4)?)(?: ?(?<value>[1-9]|s(?:kip)?|r(?:everse)?|d(?:(?:raw ?)?2?)?))?)?$");

		private static readonly Regex Color = new Regex(@"^color (?<color>r(?:ed)?|y(?:ellow)?|g(?:reen)?|b(?:lue)?)");
		private static readonly Regex AddBot = new Regex(@"^addbot (.+?)(?: (.+))?$");
		private static readonly Regex RemoveBot = new Regex(@"^removebot (.+)$");
		private static readonly Regex RenameBot = new Regex(@"^renamebot (.+) (.+?)");

		[FunctionName("SlashCommand")]
		public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)]
			HttpRequestMessage req, TraceWriter log)
		{
			log.Info("C# HTTP trigger function processed a request.");

			var reqContent = await req.Content.ReadAsStringAsync();
			var values = HttpUtility.ParseQueryString(reqContent);
			var reqData =
				JsonConvert.DeserializeObject<SlackRequest>(
					new JavaScriptSerializer().Serialize(values.AllKeys.ToDictionary(x => x, x => values[x])));

			var uno = new UnoService(reqData, log);

			string color;

			string playerName;
			switch (reqData.Text)
			{
				case var text when new Regex(@"^$").IsMatch(text):
					uno.BeginTurnInteractive();
					break;
				case var text when new Regex(@"^new$").IsMatch(text):
					uno.InitializeGame();
					break;
				case var text when Play.IsMatch(text):
					var playCapture = Play.Match(text).Groups;

					color = playCapture["color"].Value;
					var value = playCapture["value"].Value;

					uno.PlayCard(color, value);
					break;
				case var text when Color.IsMatch(text):
					var colorCapture = Color.Match(text).Groups;

					color = colorCapture["color"].Value;

					uno.SetWildColor(color);
					break;
				case var text when Regex.IsMatch(text, @"^reset thisisthepassword$"):
					uno.ResetGame();
					break;
				case var text when Regex.IsMatch(text, @"^join"):
					uno.JoinGame();
					break;
				case var text when Regex.IsMatch(text, @"^quit"):
					uno.QuitGame();
					break;
				case var text when Regex.IsMatch(text, @"^status"):
					uno.ReportStatus();
					break;
				case var text when Regex.IsMatch(text, @"^start"):
					uno.BeginGame();
					break;
				case var text when Regex.IsMatch(text, @"^draw"):
					uno.DrawCard();
					break;
				case var text when AddBot.IsMatch(text):
					var addBotCapture = AddBot.Match(text).Groups;

					var aiName = addBotCapture["aiName"].Value;
					playerName = addBotCapture["playerName"].Value;

					uno.AddAiPlayer(aiName, playerName);
					break;
				case var text when RemoveBot.IsMatch(text):
					var removeBotCapture = RemoveBot.Match(text).Groups;

					playerName = removeBotCapture["playerName"].Value;

					uno.QuitGame(playerName);
					break;
				case var text when RenameBot.IsMatch(text):
					var renameBotCapture = RenameBot.Match(text).Groups;

					playerName = renameBotCapture["playerName"].Value;
					var newPlayerName = renameBotCapture["newPlayerName"].Value;

					uno.RenameAiPlayer(playerName, newPlayerName);
					break;
			}

			return req.CreateResponse(HttpStatusCode.OK);
		}

		[FunctionName("Action")]
		public static async Task<HttpResponseMessage> Action(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)]
			HttpRequestMessage req, TraceWriter log)
		{
			log.Info("C# HTTP trigger function processed a request.");

			var reqContent = await req.Content.ReadAsStringAsync();
			var values = HttpUtility.ParseQueryString(reqContent);
			var reqData =
				JsonConvert.DeserializeObject<SlackActionRequest>(
					new JavaScriptSerializer().Serialize(values.AllKeys.ToDictionary(x => x, x => values[x])));

			var uno = new UnoService(reqData, log);

			string color;

			switch (reqData.Actions.FirstOrDefault()?.Value)
			{
				case "color":
					color = "";

					uno.SetWildColor(color);
					break;
				case "play":
					color = "";
					var value = "";

					uno.PlayCard(color, value);
					break;
				case "draw":
					uno.DrawCard();
					break;
				case "status":
					uno.ReportStatus();
					break;
				case "dismiss":
					new SlackClient(Environment.GetEnvironmentVariable("SlackWebhookUrl")).PostMessage(new SlackMessage
					{
						Text = "",
						DeleteOriginal = true
					});
					break;
			}

			return req.CreateResponse(HttpStatusCode.OK);
		}
	}
}