using System.Collections.Generic;

namespace SlackUnobot.Objects
{
	public class Card
	{
		private static readonly Dictionary<string, string> ColorMapping = new Dictionary<string, string>
		{
			{"HEARTS", "red"},
			{"SPADES", "green"},
			{"CLUBS", "yellow"},
			{"DIAMONDS", "blue"}
		};

		private static readonly Dictionary<string, string> ValueMapping = new Dictionary<string, string>
		{
			{"JACK", "draw 2"},
			{"QUEEN", "skip"},
			{"KING", "reverse"}
		};

		public string Color { get; set; }
		public string Value { get; set; }

		public static Card FromRegularCard(Card card)
		{
			var newCard = new Card();

			if (card.Value == "ACE")
			{
				newCard.Value = "wild";
				switch (card.Color)
				{
					case "CLUBS":
					case "SPADES":
						newCard.Color = "wild";
						break;
					case "HEARTS":
					case "DIAMONDS":
						newCard.Color = "draw 4";
						break;
				}
			}
			else
			{
				newCard.Color = ColorMapping[card.Color];
				newCard.Value = ValueMapping.ContainsKey(card.Value) 
					? ValueMapping[card.Value] 
					: (int.Parse(card.Value) - 1).ToString();
			}

			return newCard;
		}
	}
}