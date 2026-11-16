namespace BaneMenu.modules.troll
{
	internal class AutoReportBodies : Module
	{
		public AutoReportBodies() : base("AutoReportBodies") { }

		// The player that should be framed for reporting the body
		public PlayerControl target;

		private void OnPlayerMurder(PlayerControl murderer, PlayerControl victim, MurderResultFlags flags)
		{
			if(!flags.HasFlag(MurderResultFlags.Succeeded)) return;

			if(Utilities.IsHostEffective())
			{
				Utilities.OpenMeeting(target ?? PlayerControl.LocalPlayer, victim.Data);
				return;
			}

			if(PlayerControl.LocalPlayer.Data.IsDead) return;

			Bane.notifications.Send("Auto Report Bodies", $"{victim.Data.PlayerName} was killed by {murderer.Data.PlayerName} ({Utilities.GetPlayerColor(murderer.Data)}), their body has been automatically reported.");
			PlayerControl.LocalPlayer.CmdReportDeadBody(victim.Data);
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			EventCoordinator.OnPlayerMurder += OnPlayerMurder;
		}

		protected override void OnDisable()
		{
			target = null;

			EventCoordinator.OnPlayerMurder -= OnPlayerMurder;
		}
	}
}