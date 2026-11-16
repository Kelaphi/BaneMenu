using BaneMenu.modules;
using UnityEngine;

namespace BaneMenu.ui.sections
{
	internal class VisualSection : Section
	{
		public VisualSection() : base("Visual") { }

		public override void Render()
		{
			// SickoMenu Zoom (CameraHeight) / Max Vision / Reveal Roles
			ModuleManager.zoom.Enabled = Controls.Toggle(ModuleManager.zoom.Enabled, "Zoom");
			if (ModuleManager.zoom.Enabled)
			{
				// Sicko State.CameraHeight; orthographicSize = CameraHeight * 3
				GUILayout.Label($"Scale: {ModuleManager.zoom.Scale:0.00}x  (ortho ≈ {ModuleManager.zoom.Scale * 3f:0.0}, default 3.00 → 9)");
				ModuleManager.zoom.Scale = Controls.HorizontalSlider(ModuleManager.zoom.Scale, 0.5f, 10f);
				ModuleManager.zoom.SmoothZoom = Controls.Toggle(ModuleManager.zoom.SmoothZoom, "Smooth Zoom");
				ModuleManager.zoom.ScrollZoom = Controls.Toggle(ModuleManager.zoom.ScrollZoom, "Scroll Zoom");
				ModuleManager.zoom.ShowShadows = Controls.Toggle(ModuleManager.zoom.ShowShadows, "Show Shadows");
			}
			ModuleManager.revealRoles.Enabled = Controls.Toggle(ModuleManager.revealRoles.Enabled, "Reveal Roles");
			
			ModuleManager.skipShhhAnimation.Enabled = Controls.Toggle(ModuleManager.skipShhhAnimation.Enabled, "Skip Shhh Animation");
			ModuleManager.noSeekerAnimation.Enabled = Controls.Toggle(ModuleManager.noSeekerAnimation.Enabled, "Skip Seeker Animation");
			ModuleManager.accurateDisconnectReason.Enabled = Controls.Toggle(ModuleManager.accurateDisconnectReason.Enabled, "Use more accurate disconnection reasons");

			ModuleManager.fullbright.Enabled = Controls.Toggle(ModuleManager.fullbright.Enabled, "Fullbright (Max Vision)");
			ModuleManager.showProtections.Enabled = Controls.Toggle(ModuleManager.showProtections.Enabled, "Show Guardian Angel Protections");

			ModuleManager.alwaysVisibleChat.Enabled = Controls.Toggle(ModuleManager.alwaysVisibleChat.Enabled, "Always Visible Chat");

			ModuleManager.showGhosts.Enabled = Controls.Toggle(ModuleManager.showGhosts.Enabled, "Show Ghosts");
			ModuleManager.showGhostMessages.Enabled = Controls.Toggle(ModuleManager.showGhostMessages.Enabled, "Show messages by ghosts");

			ModuleManager.showAnonymousVotes.Enabled = Controls.Toggle(ModuleManager.showAnonymousVotes.Enabled, "Show Anonymous Votes");
			ModuleManager.realtimeVotes.Enabled = Controls.Toggle(ModuleManager.realtimeVotes.Enabled, "Show Realtime Votes");
		}
	}
}