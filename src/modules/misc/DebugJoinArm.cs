using HarmonyLib;
using BaneMenu.ui.sections;

namespace BaneMenu.modules.misc
{
	[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
	internal static class DebugJoinArm_LobbyStart
	{
		static void Postfix() => DebugSection.OnLocalJoinedLobby();
	}
}
