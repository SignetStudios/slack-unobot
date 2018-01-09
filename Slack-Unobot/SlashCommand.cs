using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using SlackUnobot.Objects.Slack;
using SlackUnobot.Services;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Script.Serialization;

namespace SlackUnobot
{
	public static class SlashCommand
	{
		[FunctionName("SlashCommand")]
		public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequestMessage req, TraceWriter log)
		{

			log.Info("C# HTTP trigger function processed a request.");

			var reqContent = await req.Content.ReadAsStringAsync();
			var values = HttpUtility.ParseQueryString(reqContent);
			var reqData = JsonConvert.DeserializeObject<SlackRequest>(new JavaScriptSerializer().Serialize(values.AllKeys.ToDictionary(x => x, x => values[x])));
			
			var uno = new UnoService(reqData, log);

			switch (reqData.Text)
			{
				case var text when new Regex(@"^$").IsMatch(text):
					uno.BeginTurnInteractive();
					break;
				case var text when new Regex(@"^new$").IsMatch(text):
					uno.InitializeGame();
					break;
				case var text when new Regex(@"^play(?: (?<color>r(?:ed)?|y(?:ellow)?|g(?:reen)?|b(?:lue)?|w(?:ild)?|d(?:raw ?4)?)(?: ?(?<value>[1-9]|s(?:kip)?|r(?:everse)?|d(?:(?:raw ?)?2?)?))?)?$").IsMatch(text):
					uno.PlayCard(color, value);
					break;
				case var text when new Regex(@"^color (?<color>r(?:ed)?|y(?:ellow)?|g(?:reen)?|b(?:lue)?)").IsMatch(text):
					uno.SetWildColor(color);
					break;
				case var text when new Regex(@"^reset thisisthepassword$").IsMatch(text):
					uno.ResetGame();
					break;
				case var text when new Regex(@"^join").IsMatch(text):
					uno.JoinGame();
					break;
				case var text when new Regex(@"^quit").IsMatch(text):
					uno.QuitGame();
					break;
				case var text when new Regex(@"^status").IsMatch(text):
					await uno.ReportHand();
					await uno.ReportTurnOrder(true);
					await uno.ReportScores(true);
					break;
				case var text when new Regex(@"^start").IsMatch(text):
					uno.BeginGame();
					break;
				case var text when new Regex(@"^draw").IsMatch(text):
					uno.DrawCard();
					break;
				case var text when new Regex(@"^addbot (.+?)(?: (.+))?$").IsMatch(text):
					uno.AddAiPlayer(aiName, playerName);
					break;
				case var text when new Regex(@"^removebot (.+)$").IsMatch(text):
					uno.QuitGame(playerName);
					break;
				case var text when new Regex(@"^renamebot (.+) (.+?)").IsMatch(text):
					uno.RenameAiPlayer(playerName, newPlayerName);
					break;
			}

			return req.CreateResponse(HttpStatusCode.OK);
		}

		[FunctionName("Action")]
		public static async Task<HttpResponseMessage> Action([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequestMessage req, TraceWriter log)
		{

			log.Info("C# HTTP trigger function processed a request.");

			var reqContent = await req.Content.ReadAsStringAsync();
			var values = HttpUtility.ParseQueryString(reqContent);
			var reqData = JsonConvert.DeserializeObject<SlackActionRequest>(new JavaScriptSerializer().Serialize(values.AllKeys.ToDictionary(x => x, x => values[x])));
			
			var uno = new UnoService(reqData, log);

			switch (reqData.Actions.FirstOrDefault()?.Value)
			{
				case "color":
					uno.SetWildColor(color);
					break;
				case "play":
					uno.PlayCard(color, value);
					break;
				case "draw":
					uno.DrawCard();
					break;
				case "status":
					await uno.ReportHand();
					await uno.ReportTurnOrder(true);
					await uno.ReportScores(true);
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
