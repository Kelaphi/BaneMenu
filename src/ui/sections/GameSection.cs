using InnerNet;
using UnityEngine;

namespace BaneMenu.ui.sections
{
	internal class GameSection : CompositeSection
	{
		public GameSection() : base(
			"Game",
			new[] { "Troll", "Chat", "Sabotage", "Lobby" },
			new Section[]
			{
				new TrollSection(),
				new ChatSection(),
				new SabotageSection(),
				new LobbySection()
			},
			"game_subtabs")
		{ }

		/// <summary>
		/// Hide Chat when not inside a game; hide Sabotage until the game has started.
		/// </summary>
		void GetVisibleSubs(out System.Collections.Generic.List<string> names, out System.Collections.Generic.List<Section> kids)
		{
			var client = AmongUsClient.Instance;
			bool inGame = client != null && client.GameState != InnerNetClient.GameStates.NotJoined;
			// Freeplay never reports GameStates.Started the same way as online matches
			bool freePlay = client != null && client.NetworkMode == NetworkModes.FreePlay;
			bool gameStarted = client != null && (
				client.GameState == InnerNetClient.GameStates.Started
				|| freePlay
				|| ShipStatus.Instance != null);

			names = new System.Collections.Generic.List<string>();
			kids = new System.Collections.Generic.List<Section>();
			for (int i = 0; i < subNames.Length; i++)
			{
				if (subNames[i] == "Chat" && !inGame) continue;
				if (subNames[i] == "Sabotage" && !gameStarted) continue;
				names.Add(subNames[i]);
				kids.Add(children[i]);
			}
		}

		public override void HandleSubsectionMove(int offset)
		{
			GetVisibleSubs(out var names, out _);
			if (names.Count == 0) return;
			int next = Mathf.Clamp(subTab + offset, 0, names.Count - 1);
			if (next != subTab)
			{
				subTab = next;
				BaneGui.ClosePopup();
				BaneGui.Unfocus();
			}
		}

		public override void Render()
		{
			GetVisibleSubs(out var names, out var kids);

			if (names.Count == 0)
			{
				GUILayout.Label("No Game options available.");
				return;
			}

			// Clamp subTab into filtered range
			if (subTab >= names.Count) subTab = names.Count - 1;
			if (subTab < 0) subTab = 0;

			int prev = subTab;
			subTab = Controls.SubTabs(names.ToArray(), subTab, subKey);
			if (subTab != prev)
			{
				BaneGui.ClosePopup();
				BaneGui.Unfocus();
			}
			GUILayout.Space(4);
			if (subTab >= 0 && subTab < kids.Count)
				kids[subTab].Render();
		}
	}
}
