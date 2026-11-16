using HarmonyLib;

namespace BaneMenu.modules.host
{
	/// <summary>
	/// Host-authoritative: reject zipline use requests.
	/// </summary>
	internal class DisableZipline : Module
	{
		public DisableZipline() : base("DisableZipline") { }

		private static DisableZipline Instance => ModuleManager.disableZipline;

		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckUseZipline))]
		class ZiplineRequest
		{
			static bool Prefix() => !Instance.Enabled;
		}
	}
}
