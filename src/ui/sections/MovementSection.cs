using BaneMenu.modules;
using System.Collections.Generic;
using UnityEngine;

namespace BaneMenu.ui.sections
{
	internal class MovementSection : Section
	{
		public MovementSection() : base("Movement") { }

		public override void Render()
		{
			if(PlayerControl.LocalPlayer == null)
			{
				GUILayout.Label("You are not currently in a game, these options will not work.");

				// Having all possible options shown at once (even if the player isn't in a game) is nice user experience
				Controls.Toggle(false, "Noclip");
			}
			else
			{
				// We don't want the position that includes the player's collider from PlayerControl::GetTruePosition()
				Vector2 position = PlayerControl.LocalPlayer.transform.position;

				GUILayout.Label($"Current Map: {Utilities.GetCurrentMap()}\nCurrent Position:\nX: {position.x:F2}\nY: {position.y:F2}");

				PlayerControl.LocalPlayer.Collider.enabled = !Controls.Toggle(!PlayerControl.LocalPlayer.Collider.enabled, "Noclip");
			}

			GUILayout.Label($"Speed Modifier: {ModuleManager.speedModifier.Multiplier:F2}x");
			ModuleManager.speedModifier.Multiplier = Controls.HorizontalSlider(ModuleManager.speedModifier.Multiplier, 0f, 5f);

			Teleporter.UseSnapToRPC = Controls.Toggle(Teleporter.UseSnapToRPC, "Use SnapTo RPC For Teleports");

			ModuleManager.nanPosition.Enabled = Controls.Toggle(ModuleManager.nanPosition.Enabled, "NaN Position (keep camera)");
			if (ModuleManager.nanPosition.Enabled)
				GUILayout.Label("Player at X/Y = NaN. Camera frozen at toggle-on position.");

			GUILayout.Label("Teleport To Location:");

			Dictionary<string, Vector2> teleportLocations = Teleporter.GetTeleportLocations();
			Controls.DrawButtonCell(teleportLocations, HandleTeleport, 2);
		}

		private void HandleTeleport(Vector2 location)
		{
			if(PlayerControl.LocalPlayer == null)
			{
				Bane.notifications.Send("Teleport", "This option can only be used inside of a game.");
				return;
			}

			Teleporter.TeleportTo(location);
		}
	}
}