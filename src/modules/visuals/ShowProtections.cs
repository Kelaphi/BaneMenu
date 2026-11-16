using HarmonyLib;

namespace BaneMenu.modules.visuals
{
	internal class ShowProtections : Module
	{
		public ShowProtections() : base("ShowProtections")
		{
			/* default off until config */ base.Enabled = false;
		}

		private static ShowProtections Instance
		{
			get { return ModuleManager.showProtections; }
		}

		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.TurnOnProtection))]
		class VisibleProtect
		{
			static void Prefix(ref bool visible)
			{
				if(Instance.Enabled) visible = true;
			}
		}
	}
}