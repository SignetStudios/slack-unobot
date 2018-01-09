using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using SlackUnobot.Objects;
using SlackUnobot.Objects.DeckOfCardsApi;
using SlackUnobot.Objects.Slack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SlackUnobot.Services
{
	public class UnoService
	{
		private readonly TraceWriter _log;
		private readonly SlackClient _slackClient;
		private readonly SlackRequest _request;
		private readonly RedisClient _redis;
		private Game _game;

		public UnoService(SlackRequest request, TraceWriter log)
		{
			_log = log;
			_request = request;
			_slackClient = new SlackClient(Environment.GetEnvironmentVariable("SlackWebhookUrl"));
			_redis = new RedisClient();
		}

		public UnoService(SlackActionRequest request, TraceWriter log) : this(request.ToSlackRequest(), log)
		{
		}

		private async Task LoadGame()
		{
			_game = await _redis.GetGameAsync(_request.ChannelId);
		}

		private const string DeckOfCardsApi = "http://deckofcardsapi.com/api";

		private static string ColorToHex(string color)
		{
			switch (color)
			{
				case "blue": return "#0033cc";
				case "red": return "#ff0000";
				case "green": return "#006633";
				case "yellow": return "#ffff00";
				case "wild": return "#000000";
				default: return "";
			}
		}

		public async Task DrawCards(string message, string playerName, int count = 1)
		{
			if (_game == null)
			{
				await LoadGame();
			}

			if (!_game.Initialized || !_game.Started)
			{
				return;
			}

			try
			{
				using (var client = new HttpClient())
				{
					var drawRequest = await client.GetStringAsync($"{DeckOfCardsApi}/deck/{_game.DeckId}/draw/?count={count}");
					var drawResult = JsonConvert.DeserializeObject<Draw>(drawRequest);
					foreach (var card in drawResult.Cards)
					{
						_game.Players[playerName].Hand.Add(Card.FromRegularCard(card));
					}

					if (drawResult.Remaining <= 10)
					{
						await SendMessage("Less than 10 cards remaining. Reshuffling the deck.");
						await client.GetStringAsync($"{DeckOfCardsApi}/deck/{_game.DeckId}/shuffle");
					}
				}
			}
			catch (Exception e)
			{
				_log.Error("An error occurred while drawing cards.", e);
				if (!_game.Players[playerName].IsAi)
				{
					await SendMessage("Sorry, something happened - I'll trade out the deck and try again.", true);
				}

				await GetNewDeck();
				//await DrawCards(message, game, playerName, count);
			}
		}

		public async Task GetNewDeck()
		{
			using (var client = new HttpClient())
			{
				var deckRequest = await client.GetStringAsync($"{DeckOfCardsApi}/deck/new/shuffle/?deck_count=2");
				var deckResult = JsonConvert.DeserializeObject<Shuffle>(deckRequest);
				_game.DeckId = deckResult.DeckId;
			}
		}

		public async Task EndTurn()
		{
			if (_game == null)
			{
				await LoadGame();
			}

			if (!_game.Initialized || !_game.Started)
			{
				return;
			}


			if (!_game.Started)
			{
				await SendMessage("This game has not yet been started.", true);
				return;
			}

			_log.Info($"Ending turn for {_game.TurnOrder.Peek()}.");
			_game.TurnOrder.Enqueue(_game.TurnOrder.Dequeue());
		}

		public Game NewGame()
		{
			return new Game();
		}

		public async Task ReportCurrentCard(bool isPrivate = false)
		{
			if (_game == null)
			{
				await LoadGame();
			}

			if (!_game.Initialized || !_game.Started)
			{
				return;
			}

			var message = new SlackMessage
			{
				Text = "The current up card is:",
				Attachments = new List<Attachment>
				{
					new Attachment
					{
						Color = ColorToHex(_game.CurrentCard.Color),
						Text = $"{_game.CurrentCard.Color} {_game.CurrentCard.Value}"
					}
				}
			};

			await SendMessage(message, isPrivate);
		}

		public async Task AnnounceTurn()
		{
			if (_game == null)
			{
				await LoadGame();
			}

			if (!_game.Initialized || !_game.Started)
			{
				return;
			}

			await ReportCurrentCard();

			var currentPlayer = _game.TurnOrder.Peek();

			await SendMessage($"Is is @{currentPlayer} 's turn.{(!_game.Players[currentPlayer].IsAi ? "\nType `/uno` to begin your turn." : "")}");
		}

		private async Task SendMessage(SlackMessage message, bool isPrivate = false)
		{
			message.LinkNames = true;
			message.Username = "Unobot";
			message.AsUser = true;
			message.Channel = _request.ChannelId;
			message.User = _request.UserId;

			if (isPrivate)
			{
				await _slackClient.PostEphemeralAsync(message);
			}
			else
			{
				await _slackClient.PostMessageAsync(message);
			}
		}

		private async Task SendMessage(string message, bool isPrivate = false)
		{
			await SendMessage(new SlackMessage
			{
				Text = message
			}, isPrivate);
		}

		private async Task SaveGame()
		{
			await _redis.SaveGameAsync(_request.ChannelId, _game);
		}

		private async Task EndGame()
		{
			if (_game == null)
			{
				await LoadGame();
			}

			if (!_game.Started)
			{
				return;
			}

			var winner = _game.TurnOrder.First();
			var points = await CalculatePoints();

			await SendMessage($"{winner} played their final card.");
			await SendMessage($"{winner} has won the hand and receives {points} points.");

			await EndTurn();

			_game.Players[winner].Score += points;

			await ReportScores();
			var currentScores = new List<(string name, int score)>();

			foreach (var player in _game.Players)
			{
				player.Value.Hand = new List<Card>();
				currentScores.Add((player.Key, player.Value.Score));
			}

			var gameWinner = currentScores.FirstOrDefault(x => x.score >= 500);

			if (!gameWinner.Equals(default(ValueTuple<string, int>)))
			{
				await SendMessage($"{gameWinner.name} has won the game with {gameWinner.score} points!");
				_game = new Game
				{
					Id = _request.ChannelId
				};
			}
			else
			{
				_game.Started = false;
				await SendMessage($"@{_game.Player1}, type `/uno start` to begin a new hand.");

				if (_game.NextGame != null && _game.NextGame.Any())
				{
					foreach (var player in _game.NextGame)
					{
						_game.TurnOrder.Enqueue(player.Name);
						await SendMessage($"{player.Name} has joined the game.");
					}

					_game.NextGame = new List<Player>();
				}
			}

			await SaveGame();
		}

		private async Task<int> CalculatePoints()
		{
			if (_game == null)
			{
				await LoadGame();
			}

			//Winning player should not have any cards in their hand
			return _game.Players
				.SelectMany(player => player.Value.Hand)
				.Sum(card => card.PointValue());
		}

		public async Task ReportScores(bool isPrivate = false)
		{
			if (_game == null)
			{
				await LoadGame();
			}

			var message = new StringBuilder("Current score:\n");

			foreach (var score in _game.Players.Select(x => (name: x.Key, score: x.Value.Score)).OrderBy(x => x.score))
			{
				message.Append($"\n{score.name}: {score.score}");
			}

			await SendMessage(message.ToString(), isPrivate);
		}

		public async Task ReportTurnOrder(bool isPrivate = false)
		{
			if (_game == null)
			{
				await LoadGame();
			}

			if (_game.Started && isPrivate)
			{
				await ReportCurrentCard(true);
			}

			var message = new StringBuilder("Current playing order:\n");
			var i = 0;

			foreach (var player in _game.TurnOrder.Select(x => _game.Players[x]))
			{
				i++;
				if (i > 1)
				{
					message.Append(", ");
				}

				message.Append($"\n{i}. {player.Name}");

				if (_game.Started)
				{
					message.Append($" ({player.Hand.Count} cards)");
				}
			}

			await SendMessage(message.ToString(), isPrivate);

			if (_game.NextGame.Any())
			{
				i = 0;
				message = new StringBuilder("Players waiting for the next hand:\n");
				foreach (var player in _game.NextGame)
				{
					i++;
					if (i > 1)
					{
						message.Append(", ");
					}

					message.Append($"\n{player.Name}");
				}

				await SendMessage(message.ToString(), isPrivate);
			}
		}

		public async Task ResetGame()
		{
			_game = new Game
			{
				Id = _request.ChannelId
			};
			await SaveGame();
			await SendMessage("Game for this channel reset.", true);
		}
	}
}