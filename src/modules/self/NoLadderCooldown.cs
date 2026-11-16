namespace BaneMenu.modules.self
{
	internal class NoLadderCooldown : Module
	{
		public NoLadderCooldown() : base("NoLadderCooldown")
		{
			/* default off until config */ base.Enabled = false;
		}

		private void OnLadderUse(Ladder ladder)
		{
			ladder.CoolDown = 0.0f;
			ladder.Destination.CoolDown = 0.0f;
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			EventCoordinator.OnUseLadder += OnLadderUse;
		}

		protected override void OnDisable()
		{
			EventCoordinator.OnUseLadder -= OnLadderUse;
		}
	}
}