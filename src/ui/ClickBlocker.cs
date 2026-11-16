using System;
using System.Reflection;
using HarmonyLib;
using BepInEx.Logging;

namespace BaneMenu.ui
{
	/// <summary>
	/// Stops the game from seeing hover/clicks while the cursor is over BaneMenu.
	/// Patches are looked up by name so a renamed/missing method is simply skipped.
	/// </summary>
	internal static class ClickBlocker
	{
		const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		static bool _wasBlocked;
		static bool _loggedClear;

		static ManualLogSource Log => Bane.Log;

		public static void Install(Harmony harmony)
		{
			// Among Us' UI hover/click dispatcher (all PassiveButtons: HUD, meeting, chat, menus...)
			Patch(harmony, "PassiveButtonManager", "Update", nameof(PrefixManager));
			// Click-to-move (only patched if this method exists in your game version)
			Patch(harmony, "KeyboardJoystick", "HandleMouse", nameof(Prefix));
		}

		static void Patch(Harmony harmony, string typeName, string methodName, string prefixName)
		{
			try
			{
				var type = AccessTools.TypeByName(typeName);
				var method = type == null ? null : AccessTools.Method(type, methodName);
				if (method == null)
				{
					Log?.LogWarning("[BaneMenu] ClickBlocker: " + typeName + "." + methodName + " not found, skipped.");
					return;
				}
				harmony.Patch(method, prefix: new HarmonyMethod(typeof(ClickBlocker), prefixName));
				Log?.LogInfo("[BaneMenu] ClickBlocker: patched " + typeName + "." + methodName);
			}
			catch (Exception ex)
			{
				Log?.LogWarning("[BaneMenu] ClickBlocker: could not patch " + typeName + "." + methodName + ": " + ex.Message);
			}
		}

		// plain blocker: returning false skips the game's own method
		public static bool Prefix()
		{
			return !MainUI.MouseBlocked();
		}

		// the button manager also needs to "un-hover" whatever it was hovering the moment the cursor
		// enters the menu, because once we skip its Update it would never send the mouse-out itself
		public static bool PrefixManager(object __instance)
		{
			bool blocked = MainUI.MouseBlocked();
			if (blocked && !_wasBlocked) ClearHover(__instance);
			_wasBlocked = blocked;
			return !blocked;
		}

		// ------------------------------------------------------------------ hover clearing
		static void ClearHover(object manager)
		{
			if (manager == null) return;
			try
			{
				bool cleared = ClearMembers(manager, true);
				if (!cleared) ClearMembers(manager, false); // fallback: any element-typed member that isn't "down"/"click"

				if (!_loggedClear)
				{
					_loggedClear = true;
					Log?.LogInfo("[BaneMenu] ClickBlocker: hover cleared when entering menu (found hovered element: " + cleared + ")");
				}
			}
			catch (Exception ex)
			{
				if (!_loggedClear)
				{
					_loggedClear = true;
					Log?.LogWarning("[BaneMenu] ClickBlocker: could not clear hover: " + ex.Message);
				}
			}
		}

		static bool ClearMembers(object manager, bool hoverNamesOnly)
		{
			bool any = false;
			var type = manager.GetType();

			foreach (var prop in type.GetProperties(Any))
			{
				if (!prop.CanRead || prop.GetIndexParameters().Length > 0 || !IsUiElement(prop.PropertyType)) continue;
				if (!NameOk(prop.Name, hoverNamesOnly)) continue;

				object el = null;
				try { el = prop.GetValue(manager); } catch { }
				if (el == null) continue;

				CallMouseOut(el);
				if (prop.CanWrite) { try { prop.SetValue(manager, null); } catch { } }
				any = true;
			}

			foreach (var field in type.GetFields(Any))
			{
				if (!IsUiElement(field.FieldType) || !NameOk(field.Name, hoverNamesOnly)) continue;

				object el = null;
				try { el = field.GetValue(manager); } catch { }
				if (el == null) continue;

				CallMouseOut(el);
				try { field.SetValue(manager, null); } catch { }
				any = true;
			}
			return any;
		}

		static bool NameOk(string name, bool hoverNamesOnly)
		{
			string n = name.ToLowerInvariant();
			if (hoverNamesOnly) return n.Contains("over") || n.Contains("hover");
			return !(n.Contains("down") || n.Contains("click") || n.Contains("drag") || n.Contains("focus"));
		}

		static bool IsUiElement(Type t)
		{
			for (; t != null; t = t.BaseType)
				if (t.Name == "PassiveUiElement" || t.Name == "PassiveButton") return true;
			return false;
		}

		static void CallMouseOut(object element)
		{
			MethodInfo chosen = null;
			foreach (var m in element.GetType().GetMethods(Any))
			{
				if (m.GetParameters().Length != 0) continue;
				if (m.Name.IndexOf("MouseOut", StringComparison.OrdinalIgnoreCase) < 0) continue;
				if (chosen == null || m.Name.StartsWith("Receive")) chosen = m;
			}
			if (chosen == null) return;
			try { chosen.Invoke(element, null); } catch { }
		}
	}
}
