using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SlackUnobot.Services
{
	public class AiService
	{
		private static readonly List<string> RequiredUrls = new List<string>
		{
			"Play"
		};

		public AiService()
		{
		}

		public async Task<bool> IsValidAi(string url)
		{
			try
			{
				using (var client = new HttpClient())
				{
					var result = JsonConvert.DeserializeObject<UrlList>(await client.GetStringAsync(url));

					return RequiredUrls.All(x => result.Urls.Any(y => y.Description == x));
				}
			}
			catch
			{
				return false;
			}
		}

		public async Task<AiResult> GetAiTurn(string url)
		{
			try
			{
				using (var client = new HttpClient())
				{
					var urls = JsonConvert.DeserializeObject<UrlList>(await client.GetStringAsync(url)).Urls;

					var playUrl = urls.FirstOrDefault(x => x.Description == "Play")?.Url;

					var result = JsonConvert.DeserializeObject<AiResult>(await client.GetStringAsync(playUrl));

					return result;
				}
			}
			catch
			{
				return AiResult.Error();
			}
		}
	}

	public class UrlList
	{
		public List<UrlElement> Urls { get; set; }
	}

	public class UrlElement
	{
		public string Description { get; set; }
		public string Url { get; set; }
	}

	public class AiResult
	{
		private AiResult()
		{
		}

		private AiResult(string color, string value)
		{
			CardColor = color;
			CardValue = value;
		}

		public AiPlayType Action { get; private set; }
		public string CardColor { get; private set; }
		public string CardValue { get; private set; }

		public static AiResult Play(string color, string value)
		{
			return new AiResult(color, value)
			{
				Action = AiPlayType.Play
			};
		}

		public static AiResult Draw()
		{
			return new AiResult
			{
				Action = AiPlayType.Draw
			};
		}

		public static AiResult Error()
		{
			return new AiResult
			{
				Action = AiPlayType.Error
			};
		}
	}

	public enum AiPlayType
	{
		Error = 0,
		Play = 1,
		Draw = 2
	}
}