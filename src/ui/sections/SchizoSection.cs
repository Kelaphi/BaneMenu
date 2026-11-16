using BaneMenu.modules.troll;
using System.Collections.Generic;
using UnityEngine;

namespace BaneMenu.ui.sections
{
	/// <summary>
	/// Hydralum/Malum Schizo tab - client-sided fake sabotages and doors (BaneMenu style).
	/// </summary>
	internal class SchizoSection : Section
	{
		readonly HashSet<byte> selected = new HashSet<byte>();
		bool selectAll;

		public SchizoSection() : base("Schizo") { }

		static IEnumerable<PlayerControl> AllPlayers()
		{
			// Prefer GameData - reliable in lobby and game
			if (GameData.Instance != null && GameData.Instance.AllPlayers != null)
			{
				foreach (NetworkedPlayerInfo info in GameData.Instance.AllPlayers)
				{
					if (info == null || info.Disconnected) continue;
					PlayerControl pc = info.Object;
					if (pc != null)
						yield return pc;
					else if (info.PlayerId == (PlayerControl.LocalPlayer != null ? PlayerControl.LocalPlayer.PlayerId : (byte)255))
					{
						if (PlayerControl.LocalPlayer != null)
							yield return PlayerControl.LocalPlayer;
					}
				}
				yield break;
			}
			if (PlayerControl.AllPlayerControls != null)
			{
				foreach (PlayerControl p in PlayerControl.AllPlayerControls)
					if (p != null) yield return p;
			}
		}

		public override void Render()
		{
			GUILayout.Label("Client-sided only - targets see fakes; host is never targeted.");
			GUILayout.Space(4);

			var players = new List<PlayerControl>();
			foreach (var p in AllPlayers())
				players.Add(p);

			if (players.Count == 0)
			{
				GUILayout.Label("No players found. Join a lobby or game first.");
				return;
			}

			if (Controls.Button(selectAll ? "Deselect All" : "Select All"))
			{
				selectAll = !selectAll;
				selected.Clear();
				if (selectAll)
				{
					foreach (PlayerControl p in players)
					{
						if (p == null || p.AmOwner) continue;
						if (Schizo.IsHost(p)) continue;
						selected.Add(p.PlayerId);
					}
				}
			}

			GUILayout.Space(4);
			GUILayout.Label("--- Targets ---");
			int listed = 0;
			foreach (PlayerControl p in players)
			{
				if (p == null || p.Data == null) continue;
				listed++;
				string name = p.Data.PlayerName ?? $"Player {p.PlayerId}";
				bool host = Schizo.IsHost(p);
				bool self = p.AmOwner;
				if (self) name += " (You)";
				if (host) name += " [HOST]";

				if (self || host)
				{
					GUILayout.Label(name + " - not targetable");
					selected.Remove(p.PlayerId);
					continue;
				}

				bool on = selected.Contains(p.PlayerId);
				bool next = Controls.Toggle(on, name, "schizo_pl_" + p.PlayerId);
				if (next != on)
				{
					if (next) selected.Add(p.PlayerId);
					else selected.Remove(p.PlayerId);
				}
			}
			if (listed == 0)
				GUILayout.Label("No player data yet.");

			List<PlayerControl> targets = new List<PlayerControl>();
			foreach (PlayerControl p in players)
			{
				if (p != null && !p.AmOwner && selected.Contains(p.PlayerId) && !Schizo.IsHost(p))
					targets.Add(p);
			}

			GUILayout.Space(6);
			GUILayout.Label($"Selected: {targets.Count}");
			bool can = targets.Count > 0 && ShipStatus.Instance != null;
			if (targets.Count == 0)
				GUILayout.Label("Select at least one non-host player.");
			else if (ShipStatus.Instance == null)
				GUILayout.Label("Fake sabotages need an active match (ship loaded). Selection works in lobby.");

			GUILayout.Space(4);
			GUILayout.Label("--- Fake sabotages ---");
			if (Controls.Button("Fake Reactor") && can)
				foreach (var t in targets) Schizo.FakeReactor(t);
			if (Controls.Button("Fake Oxygen") && can)
				foreach (var t in targets) Schizo.FakeOxygen(t);
			if (Controls.Button("Fake Comms") && can)
				foreach (var t in targets) Schizo.FakeComms(t);
			if (Controls.Button("Fake Mushroom Mixup") && can)
				foreach (var t in targets) Schizo.FakeMushroom(t);

			GUILayout.Space(4);
			GUILayout.Label("--- Fake doors ---");
			if (Controls.Button("Close All Doors (client)") && can)
				foreach (var t in targets) Schizo.CloseAllDoors(t);

			if (ShipStatus.Instance != null && ShipStatus.Instance.AllDoors != null)
			{
				var seen = new HashSet<SystemTypes>();
				foreach (OpenableDoor door in ShipStatus.Instance.AllDoors)
				{
					if (door == null || !seen.Add(door.Room)) continue;
					var room = door.Room;
					if (Controls.Button($"Close: {room}") && can)
						foreach (var t in targets) Schizo.CloseDoor(t, room);
				}
			}
		}
	}
}
