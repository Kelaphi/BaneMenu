namespace BaneMenu.modules.misc
{
	internal class ChatLogger : Module
	{
		public ChatLogger() : base("ChatLogger")
		{
			/* default off until config */ base.Enabled = false;
		}

		private void OnPlayerChat(PlayerControl sender, string text)
		{
			if(sender.Data == null) return;

			Bane.Log.LogMessage($"[ChatLogger] {sender.Data.PlayerName}: {text}");
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			EventCoordinator.OnPlayerChat += OnPlayerChat;
		}

		protected override void OnDisable()
		{
			EventCoordinator.OnPlayerChat -= OnPlayerChat;
		}
	}
}