namespace BaneMenu.routines
{
	/// <summary>
	/// Spam CmdCheckColor(255) every Update tick to try to stick Fortegreen / ??? color
	/// before the game corrects to a valid id. May or may not replicate to others.
	/// </summary>
	public class FortegreenSpamRoutine : Routine
	{
		public FortegreenSpamRoutine() : base("FortegreenSpam") { }

		public byte ColorId { get; set; } = 255;

		public override void Run()
		{
			if (PlayerControl.LocalPlayer == null) return;
			if (AmongUsClient.Instance == null) return;

			try
			{
				PlayerControl.LocalPlayer.CmdCheckColor(ColorId);
			}
			catch
			{
				// ignore per-frame failures
			}
		}
	}
}
