using System;
using System.Configuration;
using System.Threading.Tasks;
using Microsoft.Azure.Documents.Client;
using SlackUnobot.Objects;

namespace SlackUnobot.Services
{
	public class CosmosDbService : IDataService
	{
		private readonly string _dbEndpoint;
		private readonly string _dbAuthKey;
		private readonly string _dbName;

		public CosmosDbService()
		{
			_dbEndpoint = Environment.GetEnvironmentVariable("CosmosDbEndpoint");
			_dbAuthKey = Environment.GetEnvironmentVariable("CosmosDbAuthKey");
			_dbName = Environment.GetEnvironmentVariable("CosmosDbName");
		}

		public Game GetGame(string channelId)
		{
			return GetGameAsync(channelId).Result;
		}

		public async Task<Game> GetGameAsync(string channelId)
		{
			var documentUri = UriFactory.CreateDocumentUri(_dbName, "games", channelId);

			var game = await GetDocumentClient().ReadDocumentAsync<Game>(documentUri);

			return game.Document;
		}

		public void SaveGame(string channelId, Game game)
		{
			SaveGameAsync(channelId, game).Wait();
		}

		public async Task SaveGameAsync(string channelId, Game game)
		{
			var documentUri = UriFactory.CreateDocumentUri(_dbName, "games", channelId);

			await GetDocumentClient().UpsertDocumentAsync(documentUri, game);
		}

		public async Task<Ai> GetAi(string name)
		{
			var documentUri = UriFactory.CreateDocumentUri(_dbName, "ais", name);

			var game = await GetDocumentClient().ReadDocumentAsync<Ai>(documentUri);

			return game.Document;
		}

		public async Task<bool> RegisterAi(Ai ai)
		{
			if (await GetAi(ai.Name) != null)
			{
				return false;
			}

			var documentUri = UriFactory.CreateDocumentUri(_dbName, "ai", ai.Name);

			await GetDocumentClient().CreateDocumentAsync(documentUri, ai);

			return true;
		}

		private DocumentClient GetDocumentClient()
		{
			if (string.IsNullOrWhiteSpace(_dbEndpoint))
			{
				throw new ConfigurationErrorsException("CosmosDbEndpoint is not set!");
			}

			if (string.IsNullOrWhiteSpace(_dbAuthKey))
			{
				throw new ConfigurationErrorsException("CosmosDbAuthKey is not set!");
			}

			var client = new DocumentClient(new Uri(_dbEndpoint), _dbAuthKey);

			return client;
		}
	}
}