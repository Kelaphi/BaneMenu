using AmongUs.GameOptions;
using System.Collections.Generic;

namespace BaneMenu.assets
{
	internal class GameAssets
	{
		public static readonly List<RoleTypes> RolesList = new List<RoleTypes>()
		{
			RoleTypes.Crewmate,
			RoleTypes.Impostor,
			RoleTypes.Scientist,
			RoleTypes.Engineer,
			RoleTypes.GuardianAngel,
			RoleTypes.Shapeshifter,
			RoleTypes.CrewmateGhost,
			RoleTypes.ImpostorGhost,
			RoleTypes.Noisemaker,
			RoleTypes.Phantom,
			RoleTypes.Tracker,
			RoleTypes.Detective,
			RoleTypes.Viper,
		};

		public static readonly SortedList<int, string> PlayerPlatforms = new SortedList<int, string>()
		{
			{ 0, "Unknown" },
			{ 1, "Epic Games" },
			{ 2, "Steam" },
			{ 3, "MacOS" },
			{ 4, "Microsoft Store" },
			{ 5, "Itch.io" },
			{ 6, "iOS" },
			{ 7, "Android" },
			{ 8, "Nintendo Switch" },
			{ 9, "Xbox" },
			{ 10, "PlayStation" },
			{ 112, "Starlight" }
		};

		public static string GetPlatformName(Platforms platformId)
		{
			return PlayerPlatforms.GetValueOrDefault((int)platformId, $"Unknown {platformId}");
		}

		public static string GetPlatformName(int platformId)
		{
			return PlayerPlatforms.GetValueOrDefault(platformId, $"Unknown {platformId}");
		}
	}
}
