using UnityEngine;

namespace BaneMenu.ui.sections
{
	/// <summary>
	/// Test tab demonstrating SubTabs (General / Roles).
	/// </summary>
	internal class TestSection : Section
	{
		static readonly string[] SubTabNames = { "General", "Roles" };

		int subTab;

		public TestSection() : base("Test") { }

		public override void HandleSubsectionMove(int offset)
		{
			subTab = Mathf.Clamp(subTab + offset, 0, SubTabNames.Length - 1);
			BaneGui.ClosePopup();
			BaneGui.Unfocus();
		}

		public override void Render()
		{
			subTab = Controls.SubTabs(SubTabNames, subTab, "test_subtabs");
			GUILayout.Space(4);

			if (subTab == 0)
				RenderGeneral();
			else
				RenderRoles();
		}

		void RenderGeneral()
		{
			GUILayout.Label("--- SubTab: General ---");
			GUILayout.Label("This is the General sub-tab inside Test.");
			GUILayout.Label("Use the pills above to switch between General and Roles.");
			GUILayout.Space(6);
			GUILayout.Label("SubTabs keep dropdowns/textboxes closed when you switch.");
		}

		void RenderRoles()
		{
			GUILayout.Label("--- SubTab: Roles ---");
			GUILayout.Label("This is the Roles sub-tab inside Test.");
			GUILayout.Label("You can put role-related test controls here later.");
		}
	}
}
