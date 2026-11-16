using BaneMenu.fabricator;
using UnityEngine;

namespace BaneMenu.ui.sections
{
	/// <summary>
	/// Client-side multi-player macros.
	/// Create a macro → record players into it (ghosts of earlier players play while you record) → play full macro.
	/// </summary>
	internal class FabricatorSection : Section
	{
		public FabricatorSection() : base("Fabricator") { }

		private string newMacroName = "Macro";

		public override void Render()
		{
			GUILayout.Label("FABRICATOR - client-side only");
			GUILayout.Label("Macros hold multiple players. Record one player at a time;");
			GUILayout.Label("earlier players replay as local ghosts while you record the next.");
			GUILayout.Space(8);

			// ---- Create ----
			GUILayout.Label("--- Create New Macro ---");
			bool commit;
			newMacroName = Controls.TextBox("Name", newMacroName, "fab_new_macro_name", out commit, 32);
			if (Controls.Button("Create New Macro"))
			{
				FabricatorEngine.CreateNewMacro(newMacroName);
			}

			GUILayout.Space(8);

			// ---- Select ----
			GUILayout.Label("--- Current Macro Selected ---");
			var lib = FabricatorEngine.Library;
			if (lib.Count == 0)
			{
				GUILayout.Label("(no macros - create one)");
			}
			else
			{
				FabricatorEngine.selectedMacroIndex = Mathf.Clamp(
					FabricatorEngine.selectedMacroIndex, 0, lib.Count - 1);

				// Dropdown-style slider + labels
				var sel = lib[FabricatorEngine.selectedMacroIndex];
				GUILayout.Label($"Selected: [{FabricatorEngine.selectedMacroIndex}] {sel.name}");
				GUILayout.Label($"Players in macro: {sel.players.Count}   Duration: {sel.Duration:F1}s");

				if (lib.Count > 1)
				{
					FabricatorEngine.selectedMacroIndex = (int)Controls.HorizontalSlider(
						FabricatorEngine.selectedMacroIndex, 0, lib.Count - 1);
				}

				// List player slots
				for (int i = 0; i < sel.players.Count; i++)
				{
					var pt = sel.players[i];
					if (pt == null) continue;
					GUILayout.Label($"  P{i + 1}: {pt.recordedName}  {pt.duration:F1}s  samples={pt.samples.Count}  events={pt.events.Count}");
				}

				if (Controls.Button("Delete Selected Macro"))
					FabricatorEngine.DeleteSelectedMacro();
			}

			GUILayout.Space(8);

			// ---- Record / Play ----
			GUILayout.Label("--- Record / Play ---");

			if (PlayerControl.LocalPlayer == null)
			{
				GUILayout.Label("Join a game to record or play.");
			}

			if (FabricatorEngine.IsRecording)
			{
				GUILayout.Label("<color=#ff6666>RECORDING PLAYER…</color>");
				GUILayout.Label($"Ghosts playing: {FabricatorEngine.ActivePlaybackCount}");
				if (Controls.Button("Stop & Save Player"))
					FabricatorEngine.StopRecording(save: true);
				if (Controls.Button("Discard Recording"))
					FabricatorEngine.StopRecording(save: false);
			}
			else
			{
				if (Controls.Button("Start New Player Recording"))
					FabricatorEngine.StartNewPlayerRecording();

				if (Controls.Button("Play Full Macro"))
					FabricatorEngine.PlayFullMacro();
			}

			if (Controls.Button("Stop Macro"))
				FabricatorEngine.StopMacro();

			GUILayout.Label($"Active ghosts: {FabricatorEngine.ActivePlaybackCount}");

			GUILayout.Space(8);
			GUILayout.Label("--- Workflow ---");
			GUILayout.Label(
				"1. Create New Macro\n" +
				"2. Start New Player Recording → walk / chat / act → Stop & Save\n" +
				"3. Start New Player Recording again (ghosts of previous players show)\n" +
				"4. Play Full Macro to replay everyone\n" +
				"Ghosts clear when the game starts (avoids crashes).");
		}
	}
}
