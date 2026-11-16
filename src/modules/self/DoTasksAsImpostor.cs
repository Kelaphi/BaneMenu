using HarmonyLib;

namespace BaneMenu.modules.self
{
	/// <summary>
	/// SickoMenu DoTasksAsImpostor: Console.CanUse forces AllowImpostor so impostors can use task consoles.
	/// </summary>
	internal class DoTasksAsImpostor : Module
	{
		public DoTasksAsImpostor() : base("Do Tasks As Impostor") { }

		static DoTasksAsImpostor Instance => ModuleManager.doTasksAsImpostor;

		[HarmonyPatch(typeof(Console), nameof(Console.CanUse))]
		class ConsoleCanUsePatch
		{
			static void Prefix(Console __instance)
			{
				if (!Instance.Enabled) return;
				if (__instance == null) return;
				try
				{
					__instance.AllowImpostor = true;
				}
				catch { }
			}
		}
	}
}
