using AmongUs.Data;
using AmongUs.GameOptions;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using BaneMenu.assets;
using BaneMenu.modules;
using BaneMenu.network;
using InnerNet;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BaneMenu.ui.sections
{
	internal class SelfSection : Section
	{
		static readonly string[] SubNames = { "General", "Visual", "Movement", "Spoofer" };
		readonly Section visual = new VisualSection();
		readonly Section movement = new MovementSection();
		readonly Section spoofer = new SpooferSection();
		int subTab;

		public SelfSection() : base("Self") { }

		public override void HandleSubsectionMove(int offset)
		{
			subTab = Mathf.Clamp(subTab + offset, 0, SubNames.Length - 1);
			BaneGui.ClosePopup();
			BaneGui.Unfocus();
		}

		private RoleTypes selectedRole = RoleTypes.Crewmate;
		private bool selectedKicker = false;
		private int roleSliderIndex = 0;

		public override void Render()
		{
			int prev = subTab;
			subTab = Controls.SubTabs(SubNames, subTab, "self_subtabs");
			if (subTab != prev) { BaneGui.ClosePopup(); BaneGui.Unfocus(); }
			GUILayout.Space(4);
			if (subTab == 1) { visual.Render(); return; }
			if (subTab == 2) { movement.Render(); return; }
			if (subTab == 3) { spoofer.Render(); return; }
			// subTab 0 = General (original Self content)

			if(PlayerControl.LocalPlayer == null || PlayerControl.LocalPlayer.Data == null)
			{
				GUILayout.Label("You are not currently in a game, these options will not work.");
			}
			else
			{
				GUILayout.Label($"Role: {PlayerControl.LocalPlayer.Data.RoleType}");
			}

			ModuleManager.doTasksAsImpostor.Enabled = Controls.Toggle(ModuleManager.doTasksAsImpostor.Enabled, "Do Tasks As Impostor");
			ModuleManager.alwaysShowTaskAnimations.Enabled = Controls.Toggle(ModuleManager.alwaysShowTaskAnimations.Enabled, "Always Show Task Animations");
			ModuleManager.immortality.Enabled = Controls.Toggle(ModuleManager.immortality.Enabled, "Become Immortal");
			ModuleManager.hideYourself.Enabled = Controls.Toggle(ModuleManager.hideYourself.Enabled, "Hide Yourself");
			ModuleManager.clickTeleport.Enabled = Controls.Toggle(ModuleManager.clickTeleport.Enabled, "Shift/Ctrl + Right-Click Teleport");
			GUILayout.Label("Shift+RMB once, Ctrl+RMB hold - Sicko-style.");
			ModuleManager.noLadderCooldown.Enabled = Controls.Toggle(ModuleManager.noLadderCooldown.Enabled, "No Ladder Cooldown");
			ModuleManager.noZiplineCooldown.Enabled = Controls.Toggle(ModuleManager.noZiplineCooldown.Enabled, "No Zipline Cooldown");
			ModuleManager.unlimitedMeetings.Enabled = Controls.Toggle(ModuleManager.unlimitedMeetings.Enabled, "Unlimited Meetings");
			ModuleManager.updateStatsFreeplay.Enabled = Controls.Toggle(ModuleManager.updateStatsFreeplay.Enabled, "Update Stats in Freeplay");

			GUILayout.Space(8);
			GUILayout.Label("Roles / combat:");
			ModuleManager.ventAsCrewmate.Enabled = Controls.Toggle(ModuleManager.ventAsCrewmate.Enabled, "Vent As Crewmate");
			ModuleManager.moveInVents.Enabled = Controls.Toggle(ModuleManager.moveInVents.Enabled, "Move In Vents");
			ModuleManager.unlockSabotageButton.SabotageAsCrewmate = Controls.Toggle(ModuleManager.unlockSabotageButton.SabotageAsCrewmate, "Sabotage As Crewmate");
			ModuleManager.unlockSabotageButton.SabotageInVents = Controls.Toggle(ModuleManager.unlockSabotageButton.SabotageInVents, "Allow Sabotaging In Vents As Imposter");
			ModuleManager.noSabotageCooldown.Enabled = Controls.Toggle(ModuleManager.noSabotageCooldown.Enabled, "No Sabotage Cooldown");
			ModuleManager.noShapeshiftAnimation.Enabled = Controls.Toggle(ModuleManager.noShapeshiftAnimation.Enabled, "Disable Shapeshift Animation");
			ModuleManager.noPhantomAnimation.Enabled = Controls.Toggle(ModuleManager.noPhantomAnimation.Enabled, "Disable Phantom Animation");

			GUILayout.Space(5);
			GUILayout.Label("No Kill Checks:");
			ModuleManager.noKillChecks.Enabled = Controls.Toggle(ModuleManager.noKillChecks.Enabled, "Enabled");
			ModuleManager.noKillChecks.KillOtherImpostors = Controls.Toggle(ModuleManager.noKillChecks.KillOtherImpostors, "Kill Other Impostors");
			ModuleManager.noKillChecks.KillAsPhantom = Controls.Toggle(ModuleManager.noKillChecks.KillAsPhantom, "Kill While Vanished");
			ModuleManager.noKillChecks.NoKillCooldown = Controls.Toggle(ModuleManager.noKillChecks.NoKillCooldown, "No Kill Cooldown (Host-only)");
			ModuleManager.noKillChecks.KillGhosts = Controls.Toggle(ModuleManager.noKillChecks.KillGhosts, "Kill Ghosts (Host-only)");
			ModuleManager.noKillChecks.KillPlayersInVents = Controls.Toggle(ModuleManager.noKillChecks.KillPlayersInVents, "Kill Players In Vents (Exit then kill)");

			// Role picker + Kicker (from previous Roles tab)
			{
				var roleOpts = new System.Collections.Generic.List<string>();
				foreach (var r in Controls.RolesList)
					roleOpts.Add(r.ToString() == "SpiritGuide" ? "Influencer" : r.ToString());
				roleOpts.Add("Kicker (CUSTOM)");
				int maxIndex = roleOpts.Count - 1;
				if (roleSliderIndex < 0) roleSliderIndex = 0;
				if (roleSliderIndex > maxIndex) roleSliderIndex = maxIndex;
				roleSliderIndex = Controls.Dropdown("Change role to", roleOpts.ToArray(), roleSliderIndex, "self_role_dd");
				if (roleSliderIndex >= Controls.RolesList.Count)
				{
					selectedKicker = true;
				}
				else
				{
					selectedKicker = false;
					selectedRole = Controls.RolesList[roleSliderIndex];
				}
			}
			GUILayout.BeginHorizontal();
			if (Controls.Button("Apply Role" + (Utilities.IsHostEffective() && !selectedKicker ? "" : " (Local)")))
			{
				if (selectedKicker)
				{
					Bane.routines.kickOnTouch.KickerRole = true;
					Bane.notifications.Send("Update Role", "Your role has been updated to Kicker (CUSTOM).", 5);
				}
				else
				{
					if (Bane.routines.kickOnTouch.KickerRole)
						Bane.routines.kickOnTouch.KickerRole = false;
					RolesSection.UpdateRole(selectedRole);
				}
			}
			GUILayout.EndHorizontal();


			if(Controls.Button("Call Meeting"))
			{
				Utilities.AttemptStartMeeting(PlayerControl.LocalPlayer, null);
			}

			if(Controls.Button("Complete All Tasks"))
			{
				PlayerControl.LocalPlayer.StartCoroutine(CompleteAllTasks().WrapToIl2Cpp());
			}

			GUILayout.Label("Task Animations:");
			GUILayout.BeginHorizontal();
			if(Controls.Button("Start Medbay Scan"))
			{
				SetScanner(true);
			}

			if(Controls.Button("Finish Medbay Scan"))
			{
				SetScanner(false);
			}
			GUILayout.EndHorizontal();

			Dictionary<string, TaskTypes> animations = MapAssets.GetAnimations();
			Controls.DrawButtonCell(animations, PlayAnimation, 2);

			GUILayout.Space(5);
			GUILayout.Label("Avatar Controls:");
			if(Controls.Button("Randomize Avatar"))
			{
				if(AmongUsClient.Instance.AmConnected)
				{
					Utilities.RandomizePlayer(true);

					Bane.notifications.Send("Player Randomizer", "Your avatar has been randomized for this game.", 5);
				}
				else
				{
					Utilities.RandomizePlayer();

					Bane.notifications.Send("Player Randomizer", "Your name and avatar has been randomized.", 5);
				}
			}

			if(Controls.Button("Randomize Color"))
			{
				PlayerControl.LocalPlayer.CmdCheckColor((byte)Utilities.GetRandomUnusedColor());
			}

			GUILayout.Space(6);
			GUILayout.Label("Fortegreen / invalid color tests:");
			Bane.routines.fortegreenSpam.Enabled = Controls.Toggle(
				Bane.routines.fortegreenSpam.Enabled,
				"Spam CmdCheckColor(255) every frame");
			if (Bane.routines.fortegreenSpam.Enabled)
				GUILayout.Label("(very spammy - may rate-limit / kick on public)");
			int wardrobeColor = -1;
			try
			{
				if (DataManager.Player != null && DataManager.Player.Customization != null)
					wardrobeColor = DataManager.Player.Customization.Color;
			}
			catch { }
			int liveColor = -1;
			if (PlayerControl.LocalPlayer != null && PlayerControl.LocalPlayer.Data != null)
				liveColor = PlayerControl.LocalPlayer.Data.DefaultOutfit.ColorId;
			GUILayout.Label($"Wardrobe Color id: {wardrobeColor}");
			GUILayout.Label($"Live outfit ColorId: {liveColor}");

			// Wardrobe only - no RPC (tests prefs path)
			if (Controls.Button("Wardrobe only → id 255"))
			{
				try
				{
					DataManager.Player.Customization.Color = 255;
					Bane.notifications.Send("Color Test", "Wardrobe set to 255 (no RPC). Rejoin or check mirror.", 5);
				}
				catch (System.Exception e)
				{
					Bane.notifications.Send("Color Test", e.Message, 4);
				}
			}
			if (Controls.Button("Wardrobe only → id 18"))
			{
				try
				{
					DataManager.Player.Customization.Color = 18;
					Bane.notifications.Send("Color Test", "Wardrobe set to 18 (no RPC).", 5);
				}
				catch (System.Exception e)
				{
					Bane.notifications.Send("Color Test", e.Message, 4);
				}
			}

			// Compare: push invalid through normal color request
			if (PlayerControl.LocalPlayer != null && Controls.Button("CmdCheckColor(255)"))
			{
				PlayerControl.LocalPlayer.CmdCheckColor(255);
				Bane.notifications.Send("Color Test", "Sent CmdCheckColor(255)", 4);
			}
			if (PlayerControl.LocalPlayer != null && Controls.Button("CmdCheckColor(18)"))
			{
				PlayerControl.LocalPlayer.CmdCheckColor(18);
				Bane.notifications.Send("Color Test", "Sent CmdCheckColor(18)", 4);
			}

			if(Controls.Button("Restore Avatar"))
			{
				PlayerControl.LocalPlayer.CmdCheckColor(DataManager.Player.Customization.Color);
				PlayerControl.LocalPlayer.RpcSetHat(DataManager.Player.Customization.Hat);
				PlayerControl.LocalPlayer.RpcSetVisor(DataManager.Player.Customization.Visor);
				PlayerControl.LocalPlayer.RpcSetSkin(DataManager.Player.Customization.Skin);
				PlayerControl.LocalPlayer.RpcSetPet(DataManager.Player.Customization.Pet);
			}

			// ---- Auth token (very bottom of Self > General) ----
			GUILayout.Space(12);
			GUILayout.Label("--- Account / Auth ---");
			if (Controls.Button("Copy Auth Token"))
			{
				CopyAuthTokenToClipboard();
			}
		}

		/// <summary>
		/// Best-effort: pull the current session auth token from EOS / account managers
		/// and put it on the system clipboard. Tries several known AU paths via reflection
		/// so minor game version differences still work.
		/// </summary>
		private static void CopyAuthTokenToClipboard()
		{
			try
			{
				string token = TryResolveAuthToken(out string source);
				if (string.IsNullOrEmpty(token))
				{
					Bane.notifications.Send("Auth Token", "Could not find an auth token (not logged in?).", 4);
					return;
				}

				GUIUtility.systemCopyBuffer = token;
				string preview = token.Length > 24 ? token.Substring(0, 12) + "…" + token.Substring(token.Length - 8) : token;
				Bane.notifications.Send("Auth Token", $"Copied ({source}): {preview}", 5);
				Bane.Log.LogInfo($"Auth token copied via {source} (len={token.Length})");
			}
			catch (System.Exception e)
			{
				Bane.notifications.Send("Auth Token", "Copy failed: " + e.Message, 4);
				Bane.Log.LogWarning("CopyAuthToken: " + e);
			}
		}

		private static string TryResolveAuthToken(out string source)
		{
			source = "none";

			// 1) EOSManager - most common Innersloth path
			try
			{
				var eosType = System.Type.GetType("EOSManager, Assembly-CSharp")
					?? FindType("EOSManager");
				if (eosType != null)
				{
					object inst = null;
					var instProp = eosType.GetProperty("Instance",
						System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy);
					if (instProp != null) inst = instProp.GetValue(null);
					if (inst == null)
					{
						var instField = eosType.GetField("Instance",
							System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy);
						if (instField != null) inst = instField.GetValue(null);
					}

					if (inst != null)
					{
						// Prefer explicit token-ish members
						string[] names =
						{
							"UserIDToken", "UserIdToken", "AuthToken", "Token",
							"ProductUserIdToken", "IdToken", "SessionTicket", "Jwt"
						};
						foreach (var n in names)
						{
							var p = eosType.GetProperty(n, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
							if (p != null)
							{
								var v = p.GetValue(inst) as string;
								if (!string.IsNullOrEmpty(v)) { source = "EOSManager." + n; return v; }
							}
							var f = eosType.GetField(n, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
							if (f != null)
							{
								var v = f.GetValue(inst) as string;
								if (!string.IsNullOrEmpty(v)) { source = "EOSManager." + n; return v; }
							}
						}

						// Methods: GetToken / GetAuthToken / CopyUserAuthToken style
						string[] methods = { "GetToken", "GetAuthToken", "GetUserAuthToken", "CopyUserAuthToken", "GetIdToken" };
						foreach (var mn in methods)
						{
							var m = eosType.GetMethod(mn, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, System.Type.EmptyTypes, null);
							if (m != null && (m.ReturnType == typeof(string) || m.ReturnType == typeof(object)))
							{
								var v = m.Invoke(inst, null) as string;
								if (!string.IsNullOrEmpty(v)) { source = "EOSManager." + mn + "()"; return v; }
							}
						}
					}
				}
			}
			catch { }

			// 2) AccountManager
			try
			{
				if (AccountManager.Instance != null)
				{
					var am = AccountManager.Instance;
					var t = am.GetType();
					string[] names = { "AuthToken", "Token", "IdToken", "SessionToken", "LoginToken" };
					foreach (var n in names)
					{
						var p = t.GetProperty(n, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
						if (p != null)
						{
							var v = p.GetValue(am) as string;
							if (!string.IsNullOrEmpty(v)) { source = "AccountManager." + n; return v; }
						}
						var f = t.GetField(n, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
						if (f != null)
						{
							var v = f.GetValue(am) as string;
							if (!string.IsNullOrEmpty(v)) { source = "AccountManager." + n; return v; }
						}
					}
				}
			}
			catch { }

			// 3) AmongUsClient / matchmaker fallbacks
			try
			{
				if (AmongUsClient.Instance != null)
				{
					var t = AmongUsClient.Instance.GetType();
					string[] names = { "AuthToken", "Token", "SecureToken" };
					foreach (var n in names)
					{
						var p = t.GetProperty(n, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
						if (p != null)
						{
							var v = p.GetValue(AmongUsClient.Instance) as string;
							if (!string.IsNullOrEmpty(v)) { source = "AmongUsClient." + n; return v; }
						}
					}
				}
			}
			catch { }

			// 4) Last resort: PUID (not a full auth token, but useful identity)
			try
			{
				if (PlayerControl.LocalPlayer != null && PlayerControl.LocalPlayer.Data != null
					&& !string.IsNullOrEmpty(PlayerControl.LocalPlayer.Data.Puid))
				{
					source = "PlayerData.Puid (not full token)";
					return PlayerControl.LocalPlayer.Data.Puid;
				}
			}
			catch { }

			return null;
		}

		private static System.Type FindType(string simpleName)
		{
			foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
			{
				try
				{
					foreach (var t in asm.GetTypes())
					{
						if (t.Name == simpleName) return t;
					}
				}
				catch { }
			}
			return null;
		}

		private IEnumerator CompleteAllTasks()
		{
			Il2CppSystem.Collections.Generic.List<PlayerTask> allTasks = PlayerControl.LocalPlayer.myTasks;

			Bane.Log.LogInfo("Completing all tasks...");
			foreach(PlayerTask task in allTasks)
			{
				if(task.IsComplete)
				{
					Bane.Log.LogInfo($"Task {task.Id} has already been completed, skipping");
					continue;
				}

				Bane.Log.LogInfo($"Sent CompleteTask RPC for task {task.Id}");
				PlayerControl.LocalPlayer.RpcCompleteTask(task.Id);

				// If we want to complete more than six tasks then a delay needs to be implemented
				// otherwise the vanilla anticheat will kick us for violating ratelimits
				yield return Effects.Wait(0.05f);
			}

			Bane.notifications.Send("Task Finisher", "All your tasks have been finished.", 5);
		}

		private void SetScanner(bool scanning)
		{
			if(PlayerControl.LocalPlayer == null)
			{
				Bane.notifications.Send("Set Scanner", "This option can only be used inside of a game.");
				return;
			}

			if(Utilities.IsAnticheatPresent() && AmongUsClient.Instance.GameState != InnerNetClient.GameStates.Started)
			{
				Bane.notifications.Send("Set Scanner", "The game must has started in order for this feature to work.");
				return;
			}

			BatchedMessage batch = new BatchedMessage();
			batch.QueueSetScanner(PlayerControl.LocalPlayer, scanning);
			batch.FinishBatch();
		}

		private void PlayAnimation(TaskTypes task)
		{
			if(PlayerControl.LocalPlayer == null)
			{
				Bane.notifications.Send("Play Animation", "This option can only be used inside of a game.");
				return;
			}

			if(ShipStatus.Instance == null)
			{
				Bane.notifications.Send("Play Animation", "There must be an instance of ShipStatus for this feature to work.");
				return;
			}

			if(Utilities.IsAnticheatPresent() && AmongUsClient.Instance.GameState != InnerNetClient.GameStates.Started)
			{
				Bane.notifications.Send("Play Animation", "The game must has started in order for this feature to work.");
				return;
			}

			RPCEmitter.SendPlayAnimation((byte)task);
		}
	}
}