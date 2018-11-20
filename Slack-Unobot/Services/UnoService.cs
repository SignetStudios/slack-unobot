using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using SlackUnobot.Objects;
using SlackUnobot.Objects.Slack;
using Action = SlackUnobot.Objects.Slack.Action;

namespace SlackUnobot.Services
{
	public class UnoService
	{
		private readonly TraceWriter _log;
		private readonly SlackClient _slackClient;
		private readonly SlackRequest _request;
		private readonly RedisClient _redis;
		private readonly CosmosDbService _cosmos;
		private readonly DeckOfCards _deckOfCards;
		private Game _game;
		private readonly AiService _aiService;

		public UnoService(SlackRequest request, TraceWriter log)
		{
			_log = log;
			_request = request;
			_slackClient = new SlackClient(Environment.GetEnvironmentVariable("SlackWebhookUrl"));
			_redis = new RedisClient();
			_cosmos = new CosmosDbService();
			_deckOfCards = new DeckOfCards();
			_aiService = new AiService();
		}

		public UnoService(SlackActionRequest request, TraceWriter log)
			: this(request.ToSlackRequest(), log)
		{
		}

		private async Task LoadGame()
		{
			_game = await _redis.GetGameAsync(_request.ChannelId);
		}

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

		private async Task DrawCards(string playerName, int count = 1)
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
				var draw = await _deckOfCards.DrawCards(_game.DeckId, count);
				foreach (var card in draw.Cards)
				{
					_game.Players[playerName].Hand.Add(Card.FromRegularCard(card));
				}

				if (draw.Remaining <= 10)
				{
					await SendMessage("Less than 10 cards remaining. Reshuffling the deck.");
					await _deckOfCards.ShuffleDeck(_game.DeckId);
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
			}
		}

		private async Task GetNewDeck()
		{
			var deckId = await _deckOfCards.NewDeck();
			_game.DeckId = deckId;
		}

		private async Task EndTurn()
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

		private Game NewGame()
		{
			return new Game();
		}

		private async Task ReportCurrentCard(bool isPrivate = false)
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

		private async Task AnnounceTurn()
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

			await SendMessage(
				$"Is is @{currentPlayer} 's turn.{(!_game.Players[currentPlayer].IsAi ? "\nType `/uno` to begin your turn." : "")}");
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

		private async Task ReportScores(bool isPrivate = false)
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

		private async Task ReportTurnOrder(bool isPrivate = false)
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

		private async Task ReportHand(List<Attachment> additionalAttachments = null)
		{
			if (_game == null)
			{
				await LoadGame();
			}

			if (!_game.Started)
			{
				await SendMessage("The game has not yet started.", true);
				return;
			}

			var attachments = new List<Attachment>();
			var colors = new List<string>
			{
				"Blue",
				"Green",
				"Red",
				"Yellow",
				"Wild"
			};
			var hand = _game.Players[_request.UserName].Hand;
			var isFirst = true;
			var attachment = new Attachment();

			foreach (var color in colors)
			{
				if (attachment.Actions.Any())
				{
					attachments.Add(attachment);
					attachment = new Attachment
					{
						CallbackId = "nothing",
						Fallback = "",
						Actions = new List<Action>()
					};
				}

				attachment.Color = ColorToHex(color);

				var handColors = hand.Where(x => string.Equals(x.Color, color, StringComparison.CurrentCultureIgnoreCase))
														 .ToList();
				if (!handColors.Any())
				{
					continue;
				}

				foreach (var card in handColors)
				{
					if (attachment.Actions.Count >= 5)
					{
						attachments.Add(attachment);
						attachment = new Attachment
						{
							Color = ColorToHex(color.ToLower()),
							CallbackId = "nothing",
							Fallback = "",
							Actions = new List<Action>()
						};

						if (isFirst)
						{
							attachment.Pretext = "Your current hand is:";
							isFirst = false;
						}
					}

					attachment.Actions.Add(new Action
					{
						Name = "card",
						Type = "button",
						Text = $"{card.Color} {card.Value}"
					});
				}
			}

			if (attachment.Actions.Any())
			{
				attachments.Add(attachment);
			}

			if (additionalAttachments?.Any() ?? false)
			{
				attachments.AddRange(additionalAttachments);
			}

			await SendMessage(new SlackMessage
			{
				Attachments = attachments
			}, true);
		}

		public async Task ReportStatus()
		{
			await ReportHand();
			await ReportTurnOrder(true);
			await ReportScores(true);
		}

		public async Task DrawCard()
		{
			if (_game == null)
			{
				await LoadGame();
			}

			if (!_game.Started)
			{
				await SendMessage("The game has not yet started.", true);
				return;
			}

			var currentPlayer = _game.TurnOrder.Peek();

			if (_request.UserName != currentPlayer)
			{
				await SendMessage("It is not your turn.", true);
				return;
			}

			await SendMessage("Drawing card.", true);
			await DrawCards(currentPlayer);
			await SendMessage($"{currentPlayer} has drawn a card.");

			await SaveGame();
			await BeginTurnInteractive();
		}

		public async Task InitializeGame()
		{
			if (_game == null)
			{
				await LoadGame();
			}

			if (_game.Initialized)
			{
				await SendMessage("There is already a game in progress. Type `/uno join` to join the game.", true);
				return;
			}

			_game = new Game
			{
				Id = _request.ChannelId,
				Initialized = true,
				Player1 = _request.UserName,
				Players = new Dictionary<string, Player>
				{
					{ _request.UserName, new Player() }
				},
				TurnOrder = new Queue<string>(new[] { _request.UserName })
			};

			await SendMessage($"{_request.UserName} has started UNO. Type `/uno join` to join the game.");

			await SaveGame();
			await ReportTurnOrder();
		}

		public async Task JoinGame(string userName = "")
		{
			if (_game == null)
			{
				await LoadGame();
			}

			if (string.IsNullOrWhiteSpace(userName))
			{
				userName = _request.UserName;
			}

			if (_game.TurnOrder.Contains(userName) || _game.NextGame.Any(x => x.Name == userName))
			{
				await SendMessage($"{userName} has already joined the game.", true);
				return;
			}

			var newPlayer = new Player
			{
				Name = userName
			};

			_game.Players.Add(userName, newPlayer);

			if (_game.Started)
			{
				_game.NextGame.Add(newPlayer);
				await SendMessage($"{userName} will join the next game.");
			}
			else
			{
				_game.TurnOrder.Enqueue(userName);
				await SendMessage($"{userName} has joined the game.");
				await ReportTurnOrder();
			}

			await SaveGame();
		}

		public async Task PlayCard(string color = "", string value = "")
		{
			if (color == null)
			{
				color = string.Empty;
			}

			if (value == null)
			{
				value = string.Empty;
			}

			var playerName = _request.UserName;

			if (_game == null)
			{
				await LoadGame();
			}

			if (!_game.Started)
			{
				await SendMessage("The game has not yet been started.", true);
				return;
			}

			var currentPlayer = _game.TurnOrder.Peek();

			if (playerName != currentPlayer)
			{
				await SendMessage("It is not your turn.", true);
				return;
			}

			if (string.IsNullOrWhiteSpace(color) && string.IsNullOrWhiteSpace(value))
			{
				await ReportHand();
				return;
			}

			if (!Regex.IsMatch(color, "/^(w(ild)?|d(raw?4?)?)/", RegexOptions.IgnoreCase) && string.IsNullOrWhiteSpace(value))
			{
				await SendMessage("You must specify the value of the card to be played.", true);
				return;
			}

			if (Regex.IsMatch(color, "/^d(raw?4)?/", RegexOptions.IgnoreCase))
			{
				color = "wild";
				value = "draw 4";
			}
			else if (Regex.IsMatch(color, "/^w(ild)?/", RegexOptions.IgnoreCase))
			{
				color = "wild";
				value = "wild";
			}

			color = color.ToLower();
			value = value.ToLower();

			var colors = new Dictionary<string, string>
			{
				{ "b", "blue" },
				{ "y", "yellow" },
				{ "g", "green" },
				{ "r", "red" }
			};

			if (colors.ContainsKey(color))
			{
				color = colors[color];
			}

			var values = new Dictionary<string, string>
			{
				{ "s", "skip" },
				{ "r", "reverse" },
				{ "draw2", "draw 2" },
				{ "draw", "draw 2" },
				{ "d2", "draw 2" },
				{ "d", "draw 2" }
			};

			if (values.ContainsKey(value))
			{
				value = values[value];
			}

			var player = _game.Players[playerName];

			var selectedCards = player.Hand.Where(x => x.Color == color && x.Value == value).ToList();

			if (!selectedCards.Any())
			{
				_log.Info($"{color} {value}");
				await SendMessage($"You don't have a {(color != "wild" ? $"{color} " : "")}{value}", true);
				await SaveGame();
				await BeginTurnInteractive();
				return;
			}

			var cardToPlay = selectedCards.First();

			if (!_game.PlayAnything &&
				cardToPlay.Color != "wild" &&
				cardToPlay.Color != _game.CurrentCard.Color &&
				(_game.CurrentCard.Value == "wild" ||
					_game.CurrentCard.Value == "draw 4" ||
					cardToPlay.Value != _game.CurrentCard.Value))
			{
				await SendMessage($"You cannot play a {color} {value} on a {_game.CurrentCard.Color} {_game.CurrentCard.Value}",
					true);
				await SaveGame();
				await BeginTurnInteractive();
				return;
			}

			if (_game.PlayAnything)
			{
				_game.PlayAnything = false;
			}

			player.Hand.Remove(cardToPlay);
			_game.CurrentCard = cardToPlay;

			if (cardToPlay.Color == "wild")
			{
				await SaveGame();
				var chooser = new Attachment
				{
					Fallback = "Which color would you like to select?",
					Text = "Which color would you like to select?",
					CallbackId = "color_selection",
					Actions = new List<Action>
					{
						new Action
						{
							Name = "color",
							Text = "Blue",
							Type = "button",
							Value = "blue"
						},
						new Action
						{
							Name = "color",
							Text = "Green",
							Type = "button",
							Value = "green"
						},
						new Action
						{
							Name = "color",
							Text = "Red",
							Type = "button",
							Value = "red"
						},
						new Action
						{
							Name = "color",
							Text = "Yellow",
							Type = "button",
							Value = "yellow"
						}
					}
				};

				await ReportHand(new List<Attachment>
				{
					chooser
				});
				//TODO: Begin conversation and interactively prompt for color
				/*await sendMessage(message, {
				        text: '',
				        attachments: [
				            {
				                fallback: 'Which color would you like to select?',
				                text: 'Which color would you like to select?',
				                callback_id: 'color_selection',
				                actions: [
				                    {name: 'color', text: 'Blue', type: 'button', value: 'blue' },
				                    {name: 'color', text: 'Green', type: 'button', value: 'green' },
				                    {name: 'color', text: 'Red', type: 'button', value: 'red' },
				                    {name: 'color', text: 'Yellow', type: 'button', value: 'yellow' }
				                ]
				            }
				        ]
				    }, true);*/
			}

			await SendMessage($"Playing {cardToPlay.Color} {cardToPlay.Value}", true);

			if (player.Hand.Count == 1)
			{
				await SendMessage($"{playerName} only has one card left in their hand!");
			}
			else if (!player.Hand.Any())
			{
				await EndGame();
				return;
			}

			if (cardToPlay.Value == "skip" || cardToPlay.Value == "reverse" && _game.TurnOrder.Count == 2)
			{
				await EndTurn();
				await EndTurn();
			}
			else if (cardToPlay.Value == "reverse")
			{
				_game.TurnOrder = new Queue<string>(_game.TurnOrder.Reverse());
			}
			else if (cardToPlay.Value == "draw 2")
			{
				await EndTurn();
				await DrawCards(_game.TurnOrder.First(), 2);
				await EndTurn();
			}
			else
			{
				await EndTurn();
			}


			await SaveGame();
			await ReportHand();
			await SendMessage($"{playerName} played a {color} {value}");
			await AnnounceTurn();

			if (playerName == _game.TurnOrder.First())
			{
				await BeginTurnInteractive();
			}
			else
			{
				await ProcessAiTurns();
			}
		}

		public async Task SetWildColor(string color)
		{
			if (_game == null)
			{
				await LoadGame();
			}

			var playerName = _request.UserName;

			if (!_game.Started)
			{
				await SendMessage("The game has not yet been started.", true);
				return;
			}

			var currentPlayer = _game.TurnOrder.First();

			if (playerName != currentPlayer)
			{
				await SendMessage("It is not your turn.", true);
				return;
			}

			if (_game.CurrentCard.Color != "wild")
			{
				await SendMessage("You haven't played a wild.", true);
				return;
			}


			color = color.ToLower();

			var colors = new Dictionary<string, string>
			{
				{ "b", "blue" },
				{ "y", "yellow" },
				{ "g", "green" },
				{ "r", "red" }
			};

			if (colors.ContainsKey(color))
			{
				color = colors[color];
			}

			//await message.respond(message.body.response_url, {
			//	text: 'Setting the color to ' + color,
			//	delete_original: true

			//});

			_game.CurrentCard.Color = color;

			await SendMessage($"{playerName} played a {_game.CurrentCard.Value} and chose {color} as the new color.");

			await EndTurn();

			if (_game.CurrentCard.Value == "draw 4")
			{
				await DrawCards(_game.TurnOrder.First(), 4);
				await EndTurn();
			}

			await SaveGame();
			await ReportHand();

			await AnnounceTurn();

			if (playerName == _game.TurnOrder.First())
			{
				await BeginTurnInteractive();
			}
			else
			{
				await ProcessAiTurns();
			}
		}

		public async Task BeginGame()
		{
			if (_game == null)
			{
				await LoadGame();
			}

			var user = _request.UserName;

			if (_game.Player1 != user)
			{
				await SendMessage($"Only player 1 ({_game.Player1}) can start the game.", true);
				return;
			}

			if (_game.Players.Count < 2)
			{
				await SendMessage("You need at least two players to begin playing.", true);
				return;
			}

			if (_game.Started)
			{
				await SendMessage("The game is already started.", true);
				await ReportTurnOrder(true);
				return;
			}

			_game.Started = true;

			await SendMessage("Game has started! Shuffling the deck and dealing the hands.");

			try
			{
				await GetNewDeck();
				foreach (var playerName in _game.Players)
				{
					await DrawCards(playerName.Key, 7);
				}

				//draw the starting card as well
				var startingCard = await _deckOfCards.DrawCards(_game.DeckId, 1);
				_game.CurrentCard = Card.FromRegularCard(startingCard.Cards.First());
				_game.PlayAnything = _game.CurrentCard.Color == "wild";
			}
			catch (Exception e)
			{
				_log.Error("An error occurred starting the game.", e);
				await SendMessage("An error occurred starting the game.", true);
				return;
			}

			await SaveGame();
			await AnnounceTurn();
			await ReportHand();
			await ProcessAiTurns();
		}

		private async Task ProcessAiTurns()
		{
			if (_game == null)
			{
				await LoadGame();
			}

			var nextPlayer = _game.Players[_game.TurnOrder.Peek()];

			while (nextPlayer.IsAi)
			{
				var ai = await _cosmos.GetAi(nextPlayer.AiType);

				var playResult = await _aiService.GetAiTurn(ai.Url);

				switch (playResult.Action)
				{
					case AiPlayType.Play:
						await AiPlay(playResult);
						break;
					case AiPlayType.Draw:
						await AiDraw();
						break;
				}
			}
		}

		private async Task AiPlay(AiResult result)
		{
		}

		private async Task AiDraw()
		{
		}

		public async Task QuitGame(string playerName = "")
		{
			if (_game == null)
			{
				await LoadGame();
			}

			var user = playerName;

			if (string.IsNullOrWhiteSpace(user))
			{
				user = _request.UserName;
			}

			if (!_game.TurnOrder.Contains(user))
			{
				await SendMessage($"{user} wasn't playing to begin with.", true);
				return;
			}

			if (!_game.Players[user].IsAi && user != _request.UserName)
			{
				await SendMessage($"{user} is not an AI and must leave voluntarily.", true);
				return;
			}

			_game.TurnOrder = (Queue<string>) _game.TurnOrder.Where(x => x != user); //might work?

			_game.NextGame.RemoveAll(x => x.Name == user);

			await SendMessage($"{user} has left the game.");

			if (_game.TurnOrder.Count == 0)
			{
				_game = NewGame();
				await SaveGame();
				await SendMessage("No more players. Ending the game.");
				return;
			}

			var humanPlayers = _game.TurnOrder.Where(x => !_game.Players[x].IsAi).ToList();

			if (humanPlayers.Count == 0)
			{
				_game.Started = false;
				await SaveGame();
				await SendMessage("Only AI players remaining. Waiting for more human players.");
				return;
			}

			if (_game.Player1 == user)
			{
				_game.Player1 = humanPlayers[0];
				await SendMessage($"{_game.Player1} is the new player 1.");
			}

			if (_game.TurnOrder.Count == 1)
			{
				_game.Started = false;
				await SaveGame();
				await SendMessage("Only one player remaining. Waiting for more players.");
				return;
			}

			await SaveGame();
			await ReportTurnOrder();
		}

		public async Task AddAiPlayer(string aiName, string playerName = "")
		{
			if (_game == null)
			{
				await LoadGame();
			}

			var ai = await _cosmos.GetAi(aiName);

			if (ai == null)
			{
				await SendMessage($"AI {aiName} is not registered", true);
				return;
			}

			if (!await _aiService.IsValidAi(ai.Url))
			{
				await SendMessage($"{aiName} is not a properly-defined AI.", true);
				return;
			}

			if (string.IsNullOrWhiteSpace(playerName))
			{
				playerName = ai.PreferredName;
			}

			if (string.IsNullOrWhiteSpace(playerName))
			{
				playerName = aiName;
			}

			if (_game.TurnOrder.Any(x => x == playerName) || _game.NextGame.Any(x => x.Name == playerName))
			{
				await SendMessage($"There is already a player name {playerName} playing the game.", true);
				return;
			}

			if (_game.Players[playerName] != null)
			{
				if (!_game.Players[playerName].IsAi)
				{
					await SendMessage($"There is already a player named {playerName} registered in this game.", true);
				}
			}
			else
			{
				_game.Players.Add(playerName, new Player
				{
					IsAi = true,
					AiType = aiName,
					Score = 0,
					Id = playerName
				});
			}

			if (_game.Started)
			{
				_game.NextGame.Add(_game.Players[playerName]);
				await SendMessage($"{playerName} ({aiName}.ai) will join the next hand.");
			}
			else
			{
				_game.TurnOrder.Enqueue(playerName);
				await SendMessage($"{playerName} ({aiName}.ai) has joined the game.");
				await ReportTurnOrder(true);
			}

			await SaveGame();
		}

		public async Task RenameAiPlayer(string playerName, string newPlayerName)
		{
			if (_game == null)
			{
				await LoadGame();
			}

			if (_game.Players.All(x => x.Key != playerName))
			{
				return;
			}

			if (_game.Players.Any(x => x.Key == newPlayerName))
			{
				return;
			}

			if (!_game.Players[playerName].IsAi)
			{
				return;
			}

			_game.Players[newPlayerName] = _game.Players[playerName];
			_game.Players.Remove(playerName);

			//TODO: rename in TurnOrder
			//TODO: rename in NextGame

			await SendMessage($"AI player {playerName} is now named {newPlayerName}");
		}

		public async Task BeginTurnInteractive()
		{
			if (_game == null)
			{
				await LoadGame();
			}

			var playerName = _request.UserName;

			if (!_game.Started)
			{
				await SendMessage("The game has not yet been started.", true);
				return;
			}

			var currentPlayer = _game.TurnOrder.Peek();

			if (playerName != currentPlayer)
			{
				//TODO: This will eventually be handled by showing a limited menu (status mostly)
				await SendMessage("It is not your turn.", true);
				return;
			}

			var toSend = new SlackMessage
			{
				Text = "What would you like to do?",
				Attachments = new List<Attachment>
				{
					new Attachment
					{
						Pretext = "The current up card is:",
						Color = ColorToHex(_game.CurrentCard.Color),
						Text = $"{_game.CurrentCard.Color} {_game.CurrentCard.Value}"
					}
				},
				ReplaceOriginal = false,
				DeleteOriginal = true
			};

			var colors = new List<string>
			{
				"Blue",
				"Green",
				"Red",
				"Yellow",
				"Wild"
			};

			var hand = _game.Players[playerName].Hand;
			var isFirst = true;

			foreach (var color in colors)
			{
				var handColors = hand.Where(x => x.Color == color).ToList();
				if (!handColors.Any())
				{
					continue;
				}

				var actionLists = handColors.Select((x, index) => new
																		{
																			val = new Action
																			{
																				Name = "play",
																				Text = x.Color == "Wild" ? x.Value : $"{x.Color} {x.Value}",
																				Type = "button",
																				Value = x.Color == "Wild" ? x.Value : $"{x.Color} {x.Value}"
																			},
																			index
																		})
																		.GroupBy(x => x.index / 5, y => y.val); //Grouped into sets of 5

				foreach (var actionList in actionLists)
				{
					var attachment = new Attachment
					{
						Color = ColorToHex(color.ToLower()),
						Fallback = "You are unable to play a card",
						CallbackId = "playCard",
						Actions = actionList.ToList()
					};

					if (isFirst)
					{
						attachment.Pretext = "Play a card";
						isFirst = false;
					}

					toSend.Attachments.Add(attachment);
				}
			}

			toSend.Attachments.Add(new Attachment
			{
				Fallback = "You were unable to perform the action",
				CallbackId = "other",
				Pretext = "Other Action",
				Actions = new List<Action>
				{
					new Action
					{
						Name = "draw",
						Text = "Draw a card",
						Type = "button",
						Value = "draw"
					},
					new Action
					{
						Name = "status",
						Text = "View game status",
						Type = "button",
						Value = "status"
					},
					new Action
					{
						Name = "dismiss",
						Text = "Dismiss",
						Type = "button",
						Value = "dismiss"
					}
				}
			});

			await SendMessage(toSend, true);
		}
	}
}