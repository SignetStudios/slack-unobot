using System.Threading.Tasks;
using SlackUnobot.Objects;

namespace SlackUnobot.Services
{
	public interface IDataService
	{
		Game GetGame(string channelId);
		Task<Game> GetGameAsync(string channelId);
		void SaveGame(string channelId, Game game);
		Task SaveGameAsync(string channelId, Game game);
	}
}