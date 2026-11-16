using HarmonyLib;

namespace BaneMenu.modules.visuals
{
	internal class ShowAnonymousVotes : Module
	{
		public ShowAnonymousVotes() : base("ShowAnonymousVotes")
		{
			/* default off until config */ base.Enabled = false;
		}

		private static ShowAnonymousVotes Instance
		{
			get { return ModuleManager.showAnonymousVotes; }
		}

		[HarmonyPatch(typeof(LogicOptionsNormal), nameof(LogicOptionsNormal.GetAnonymousVotes))]
		class SetAnonymousVotesDisabled
		{
			static bool Prefix(ref bool __result)
			{
				if(!Instance.Enabled) return true;

				__result = false;
				return false;
			}
		}
	}
}