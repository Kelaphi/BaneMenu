namespace BaneMenu.modules
{
	/// <summary>
	/// Menu test toggle: only affects BaneMenu feature gates via Utilities.IsHostEffective /
	/// IsAnticheatPresent - does NOT patch the game's real AmHost (that breaks lobbies).
	/// </summary>
	internal static class DevUnlock
	{
		public static bool Enabled = false;
	}
}
