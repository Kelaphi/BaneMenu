namespace BaneMenu.modules.self
{
	internal class NoZiplineCooldown : Module
	{
		public NoZiplineCooldown() : base("NoZiplineCooldown")
		{
			/* default off until config */ base.Enabled = false;
		}

		private void OnUseZipline(ZiplineConsole zipline)
		{
			zipline.CoolDown = 0.0f;
			zipline.destination.CoolDown = 0.0f;
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			EventCoordinator.OnUseZipline += OnUseZipline;
		}

		protected override void OnDisable()
		{
			EventCoordinator.OnUseZipline -= OnUseZipline;
		}
	}
}