using Il2CppInterop.Runtime;

namespace BaneMenu.modules.self
{
	internal class UnlimitedMeetings : Module
	{
		public UnlimitedMeetings() : base("UnlimitedMeetings")
		{
			/* default off until config */ base.Enabled = false;
		}

		private void OnOpenMinigame(Minigame minigame)
		{
			if(minigame.GetIl2CppType() != Il2CppType.From(typeof(EmergencyMinigame))) return;

			PlayerControl.LocalPlayer.RemainingEmergencies = 999999;
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			EventCoordinator.OnOpenMinigame += OnOpenMinigame;
		}

		protected override void OnDisable()
		{
			EventCoordinator.OnOpenMinigame -= OnOpenMinigame;

			if(PlayerControl.LocalPlayer != null)
			{
				PlayerControl.LocalPlayer.RemainingEmergencies = GameManager.Instance.LogicOptions.GetNumEmergencyMeetings();
			}
		}
	}
}