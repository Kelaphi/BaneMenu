using UnityEngine;

namespace BaneMenu.ui.sections
{
	/// <summary>
	/// Top-level tab that hosts multiple existing sections as SubTabs.
	/// </summary>
	internal class CompositeSection : Section
	{
		protected readonly string[] subNames;
		protected readonly Section[] children;
		protected readonly string subKey;
		protected int subTab;

		public CompositeSection(string name, string[] subNames, Section[] children, string subKey = null)
			: base(name)
		{
			this.subNames = subNames;
			this.children = children;
			this.subKey = subKey ?? (name.ToLowerInvariant() + "_subtabs");
		}

		public override void HandleSubsectionMove(int offset)
		{
			int next = Mathf.Clamp(subTab + offset, 0, children.Length - 1);
			if (next != subTab)
			{
				subTab = next;
				BaneGui.ClosePopup();
				BaneGui.Unfocus();
			}
		}

		public override void Render()
		{
			int prev = subTab;
			subTab = Controls.SubTabs(subNames, subTab, subKey);
			if (subTab != prev)
			{
				BaneGui.ClosePopup();
				BaneGui.Unfocus();
			}
			GUILayout.Space(4);
			if (subTab >= 0 && subTab < children.Length)
				children[subTab].Render();
		}
	}
}
