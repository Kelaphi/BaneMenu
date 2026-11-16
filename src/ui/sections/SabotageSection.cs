using System.Collections.Generic;
using UnityEngine;
// Utilities is in root BaneMenu namespace

namespace BaneMenu.ui.sections
{
	internal class SabotageSection : Section
	{
		public SabotageSection() : base("Sabotage") { }

		public override void Render()
		{
			if(ShipStatus.Instance == null)
			{
				GUILayout.Label("You are not currently in a game, or the game has not started yet. These options will not work.");
			}

			// Always update sabotage systems directly (toggle removed - always on)
			Sabotage.UpdateSystemsDirectly = true;

			GUILayout.Space(5);
			// Custom critical timer is host-only
			if (global::BaneMenu.Utilities.IsHostEffective())
			{
				Sabotage.UseCustomCriticalTimer = Controls.Toggle(
					Sabotage.UseCustomCriticalTimer,
					"Custom critical timer (next reactor/O2/heli)");
				if (Sabotage.UseCustomCriticalTimer)
				{
					GUILayout.Label($"Timer: {Sabotage.CustomCriticalTimerSeconds:F0}s (applies when YOU sabotage that system)");
					Sabotage.CustomCriticalTimerSeconds = Controls.HorizontalSlider(
						Sabotage.CustomCriticalTimerSeconds, 1f, 360f);
				}
			}
			else
			{
				Sabotage.UseCustomCriticalTimer = false;
			}

			Dictionary<string, SystemTypes> sabotages = Sabotage.GetSabotages();
			Dictionary<string, SystemTypes> doors = Sabotage.GetDoors();

			GUILayout.BeginHorizontal();
			if(Controls.Button("Sabotage All"))
			{
				Sabotage.SabotageAll();
				Bane.notifications.Send("Sabotage", "All sabotages have been enabled.", 5);
			}

			if(Controls.Button("Close All Doors"))
			{
				Sabotage.LockAll();
				Bane.notifications.Send("Sabotage", "All doors have been closed.", 5);
			}
			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();
			if(Controls.Button("Fix All Sabotages"))
			{
				Sabotage.FixAllSabotages();
				Bane.notifications.Send("Sabotage", "All sabotages have been repaired.", 5);
			}

			if(Controls.Button("Unlock All Doors"))
			{
				if(Sabotage.CanUnlockDoors())
				{
					Sabotage.UnlockAll();
					Bane.notifications.Send("Sabotage", "All doors have been unlocked.", 5);
				}
				else
				{
					Bane.notifications.Send("Sabotage", "The map you are currently on does not support unlocking doors.", 10);
				}
			}
			GUILayout.EndHorizontal();

			GUILayout.Space(5);
			GUILayout.Label("Sabotages:");
			foreach(var (key, value) in sabotages)
			{
				if(Controls.Button(key))
				{
					HandleSabotage(value);
				}
			}

			GUILayout.Label("Close Doors:");
			if(doors.Count == 0)
			{
				GUILayout.Label("This map has no doors that can be closed.");
			}
			else
			{
				Controls.DrawButtonCell(doors, HandleCloseDoor, 2);
			}
		}



		private void HandleSabotage(SystemTypes system)
		{
			if(PlayerControl.LocalPlayer == null)
			{
				Bane.notifications.Send("Sabotage", "This option can only be used inside of a game.");
				return;
			}

			if(ShipStatus.Instance == null)
			{
				Bane.notifications.Send("Sabotage", "There must be an instance of ShipStatus for this feature to work.");
				return;
			}

			Event currentEvent = Event.current;

			if(currentEvent.button == 0)
			{
				Sabotage.SabotageSystem(system);
				Bane.notifications.Send("Sabotage", $"{system} has been sabotaged.", 5);
			}
			else if(currentEvent.button == 1)
			{
				Sabotage.FixSabotage(system);
				Bane.notifications.Send("Sabotage", $"{system} has been fixed.", 5);
			}
		}

		private void HandleCloseDoor(SystemTypes door)
		{
			if(PlayerControl.LocalPlayer == null)
			{
				Bane.notifications.Send("Sabotage", "This option can only be used inside of a game.");
				return;
			}

			if(ShipStatus.Instance == null)
			{
				Bane.notifications.Send("Sabotage", "There must be an instance of ShipStatus for this feature to work.");
				return;
			}

			Event currentEvent = Event.current;

			if(currentEvent.button == 0)
			{
				Sabotage.LockDoor(door);
				return;
			}

			if(!Sabotage.CanUnlockDoors())
			{
				Bane.notifications.Send("Sabotage", "You can only unlock doors if you are the host or if the map is Polus, Airship, or Fungle.");
				return;
			}

			Sabotage.UnlockDoor(door);
		}
	}
}