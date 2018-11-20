using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SlackUnobot.Objects;
using StackExchange.Redis;

namespace SlackUnobot.Services
{
	public class RedisClient : IDataService
	{
		private readonly ConnectionMultiplexer _redis;
		private const string RedisKey = "botkit:store:channels";

		public RedisClient()
		{
			_redis = ConnectionMultiplexer.Connect(new ConfigurationOptions
			{
				EndPoints =
				{
					{
						Environment.GetEnvironmentVariable("RedisHost"),
						int.Parse(Environment.GetEnvironmentVariable("RedisPort") ?? "")
					}
				},
				Password = Environment.GetEnvironmentVariable("RedisPassword")
			});
		}

		public Game GetGame(string channelId)
		{
			var db = _redis.GetDatabase();

			return db.HashExists(RedisKey, channelId)
				? JsonConvert.DeserializeObject<Game>(db.HashGet(RedisKey, channelId))
				: new Game();
		}

		public async Task<Game> GetGameAsync(string channelId)
		{
			var db = _redis.GetDatabase();

			return await db.HashExistsAsync(RedisKey, channelId)
				? JsonConvert.DeserializeObject<Game>(await db.HashGetAsync(RedisKey, channelId))
				: new Game();
		}

		public void SaveGame(string channelId, Game game)
		{
			var db = _redis.GetDatabase();
			db.HashSet(RedisKey, channelId, JsonConvert.SerializeObject(game));
		}

		public async Task SaveGameAsync(string channelId, Game game)
		{
			var db = _redis.GetDatabase();
			await db.HashSetAsync(RedisKey, channelId, JsonConvert.SerializeObject(game));
		}
	}
}