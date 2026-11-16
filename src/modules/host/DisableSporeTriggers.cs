using HarmonyLib;

namespace BaneMenu.modules.host
{
	/// <summary>
	/// Host-authoritative: block mushroom spore trigger requests.
	/// </summary>
	internal class DisableSporeTriggers : Module
	{
		public DisableSporeTriggers() : base("DisableSporeTriggers") { }

		private static DisableSporeTriggers Instance => ModuleManager.disableSporeTriggers;

		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckSporeTrigger))]
		class TriggerSporeRequest
		{
			static bool Prefix() => !Instance.Enabled;
		}
	}
}
