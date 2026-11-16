using HarmonyLib;

namespace BaneMenu.modules.roles
{
	internal class NoShapeshiftAnimation : Module
	{
		public NoShapeshiftAnimation() : base("NoShapeshiftAnimation", [ typeof(FakeShapeshiftBubble) ]) { }

		private static NoShapeshiftAnimation Instance => ModuleManager.noShapeshiftAnimation;

		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdCheckShapeshift))]
		class StartShapeshift
		{
			static void Prefix(ref bool shouldAnimate)
			{
				if(!Instance.Enabled) return;
				shouldAnimate = false;
			}
		}

		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdCheckRevertShapeshift))]
		class RevertShapeshift
		{
			static void Prefix(ref bool shouldAnimate)
			{
				if(!Instance.Enabled) return;
				shouldAnimate = false;
			}
		}
	}
}
