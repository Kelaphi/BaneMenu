using AmongUs.GameOptions;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using BaneMenu.modules;
using InnerNet;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BaneMenu.routines
{
	public class KickOnTouchRoutine : Routine
	{
		public KickOnTouchRoutine() : base("KickOnTouch") { }

		// How close a source needs to be to trigger a kick (roughly "touching")
		public float TouchDistance { get; set; } = 0.35f;

		// Cooldown after kicking a player so we don't spam the exploit every frame
		public float KickCooldown { get; set; } = 2.5f;

		// Local player's own Kick on Touch (Troll tab) - independent from giving it to others
		private bool _localEnabled = false;
		public bool LocalEnabled
		{
			get { return _localEnabled; }
			set
			{
				if (_localEnabled == value) return;
				_localEnabled = value;
				RefreshActiveState();
			}
		}

		// Enabled is true if local is on, anyone has been given the effect, or Kicker role is on
		// This is what RoutineManager checks to call Run()
		public override bool Enabled
		{
			get { return _localEnabled || sources.Count > 0 || _kickerRole; }
			set
			{
				// External sets (and Troll tab if still bound to Enabled) only affect local
				LocalEnabled = value;
			}
		}

		// Client-side Kicker role: kill button kicks instead of murders, no cooldown
		private bool _kickerRole = false;
		public bool KickerRole
		{
			get { return _kickerRole; }
			set
			{
				if (_kickerRole == value) return;
				_kickerRole = value;

				if (value)
				{
					ApplyKickerRole();
				}
				else
				{
					RestoreOriginalRole();
				}

				RefreshActiveState();
			}
		}

		// Players who have been given the Kick on Touch effect (keyed by GetHashCode like other routines)
		public readonly HashSet<int> sources = new HashSet<int>();

		private readonly Dictionary<int, float> recentKicks = new Dictionary<int, float>();
		// After a Kicker kill, teleport to the target's last position once they disconnect
		private readonly Dictionary<int, Vector2> pendingKickerTeleports = new Dictionary<int, Vector2>();
		private RoleTypes originalRole = RoleTypes.Crewmate;
		private bool hasStoredRole = false;
		private bool eventsHooked = false;

		private static KickOnTouchRoutine Instance
		{
			get { return Bane.routines.kickOnTouch; }
		}

		// Call after mutating sources from the Players tab so the routine stays running
		public void NotifySourcesChanged()
		{
			RefreshActiveState();
		}

		private void RefreshActiveState()
		{
			bool shouldHook = _localEnabled || sources.Count > 0 || _kickerRole;

			if (shouldHook && !eventsHooked)
			{
				eventsHooked = true;
				recentKicks.Clear();
				EventCoordinator.OnDisconnect += OnDisconnect;
				EventCoordinator.OnPlayerDisconnect += OnPlayerDisconnect;
				EventCoordinator.OnGameStart += OnGameStart;
				EventCoordinator.OnGameLoad += OnGameLoad;
			}
			else if (!shouldHook && eventsHooked)
			{
				eventsHooked = false;
				recentKicks.Clear();
				EventCoordinator.OnDisconnect -= OnDisconnect;
				EventCoordinator.OnPlayerDisconnect -= OnPlayerDisconnect;
				EventCoordinator.OnGameStart -= OnGameStart;
				EventCoordinator.OnGameLoad -= OnGameLoad;
			}
		}

		public override void Run()
		{
			if (PlayerControl.LocalPlayer == null || PlayerControl.LocalPlayer.Data == null) return;

			// Force Kicker role reveal text every frame while the intro is on screen
			// (ShowRole is a coroutine - a one-shot Postfix gets overwritten by the game)
			if (KickerRole && IntroCutscene.Instance != null)
			{
				ForceKickerRevealText(IntroCutscene.Instance);
			}

			if (ShipStatus.Instance == null) return; // game must have started

			// Keep kill timer at 0 and kill button visible while Kicker is active
			if (KickerRole && !PlayerControl.LocalPlayer.Data.IsDead)
			{
				PlayerControl.LocalPlayer.SetKillTimer(0f);
				if (HudManager.Instance != null && HudManager.Instance.KillButton != null)
					HudManager.Instance.KillButton.gameObject.SetActive(true);
			}

			// Keep updating pending kicker teleport positions until the target actually disconnects
			// so we snap to where they were right before the kick lands, not where they were on button press
			if (pendingKickerTeleports.Count > 0)
			{
				foreach (PlayerControl player in PlayerControl.AllPlayerControls)
				{
					if (player == null || player.Data == null) continue;
					if (pendingKickerTeleports.ContainsKey(player.OwnerId))
					{
						pendingKickerTeleports[player.OwnerId] = player.transform.position;
					}
				}
			}

			float now = Time.time;

			// Clean up expired cooldowns
			List<int> expired = null;
			foreach (var kvp in recentKicks)
			{
				if (now - kvp.Value >= KickCooldown)
				{
					expired ??= new List<int>();
					expired.Add(kvp.Key);
				}
			}
			if (expired != null)
			{
				foreach (int id in expired)
					recentKicks.Remove(id);
			}

			// Build list of active sources
			List<PlayerControl> activeSources = new List<PlayerControl>();

			// Local player is a source only if LocalEnabled is on (and they're alive)
			if (LocalEnabled && !PlayerControl.LocalPlayer.Data.IsDead && !PlayerControl.LocalPlayer.Data.Disconnected)
			{
				activeSources.Add(PlayerControl.LocalPlayer);
			}

			// Add any players that were given the effect via Players tab
			foreach (PlayerControl player in PlayerControl.AllPlayerControls)
			{
				if (player == null || player.Data == null || player.Data.IsDead || player.Data.Disconnected) continue;
				if (player == PlayerControl.LocalPlayer) continue; // already handled
				if (sources.Contains(player.GetHashCode()))
				{
					activeSources.Add(player);
				}
			}

			if (activeSources.Count == 0) return;

			foreach (PlayerControl source in activeSources)
			{
				Vector2 sourcePos = source.GetTruePosition();

				foreach (PlayerControl target in PlayerControl.AllPlayerControls)
				{
					if (target == null || target == source) continue;
					if (target.Data == null || target.Data.IsDead || target.Data.Disconnected) continue;
					if (target.OwnerId == AmongUsClient.Instance.HostId) continue; // can't kick host
					if (Whitelist.ShouldSkip(target)) continue;
					if (recentKicks.ContainsKey(target.OwnerId)) continue;

					// Don't let sources kick each other if both have the effect
					if (sources.Contains(target.GetHashCode()) || (target == PlayerControl.LocalPlayer && LocalEnabled)) continue;

					float dist = Vector2.Distance(sourcePos, target.GetTruePosition());
					if (dist <= TouchDistance)
					{
						Bane.Log.LogInfo($"KickOnTouch: {source.Data.PlayerName} touched {target.Data.PlayerName} (dist={dist:F2}), sending kick");
						Utilities.KickPlayer(target);
						recentKicks[target.OwnerId] = now;
					}
				}
			}
		}

		private void ApplyKickerRole()
		{
			if (PlayerControl.LocalPlayer == null || PlayerControl.LocalPlayer.Data == null) return;
			if (RoleManager.Instance == null) return;

			// Store original role only once
			if (!hasStoredRole)
			{
				originalRole = PlayerControl.LocalPlayer.Data.RoleType;
				hasStoredRole = true;
			}

			// Client-side only - same method Roles tab uses (CoSetRole blocks non-ghost overrides in-game)
			HudManager.Instance.ReportButton.gameObject.SetActive(true);
			RoleManager.Instance.SetRole(PlayerControl.LocalPlayer, RoleTypes.Impostor);
			PlayerControl.LocalPlayer.SetKillTimer(0f);

			Bane.Log.LogInfo($"Kicker role applied (original role was {originalRole})");
		}

		private void RestoreOriginalRole()
		{
			if (PlayerControl.LocalPlayer == null || PlayerControl.LocalPlayer.Data == null) return;
			if (!hasStoredRole) return;
			if (RoleManager.Instance == null) return;

			bool isGhost = RoleManager.IsGhostRole(originalRole);
			HudManager.Instance.ReportButton.gameObject.SetActive(!isGhost);
			RoleManager.Instance.SetRole(PlayerControl.LocalPlayer, originalRole);

			Bane.Log.LogInfo($"Restored original role: {originalRole}");
			hasStoredRole = false;
		}

		private void OnGameStart()
		{
			if (KickerRole)
				ApplyKickerRole();
		}

		private void OnGameLoad()
		{
			if (KickerRole)
				ApplyKickerRole();
		}

		private void OnDisconnect()
		{
			sources.Clear();
			pendingKickerTeleports.Clear();
			hasStoredRole = false;
			_kickerRole = false;
			_localEnabled = false;
			RefreshActiveState();
		}

		private void OnPlayerDisconnect(ClientData client, DisconnectReasons reason)
		{
			// Teleport to last position of someone we just kicked as Kicker
			if (pendingKickerTeleports.TryGetValue(client.Id, out Vector2 pos))
			{
				// Prefer live character position if it still exists at disconnect time
				if (client.Character != null)
					pos = client.Character.transform.position;

				pendingKickerTeleports.Remove(client.Id);
				if (PlayerControl.LocalPlayer != null && PlayerControl.LocalPlayer.Data != null && !PlayerControl.LocalPlayer.Data.IsDead)
				{
					Bane.Log.LogInfo($"Kicker: target disconnected, teleporting to their last position {pos}");
					Teleporter.TeleportTo(pos);
				}
			}

			if (client.Character == null) return;
			sources.Remove(client.Character.GetHashCode());
			RefreshActiveState();
		}

		// Base OnEnable/OnDisable are unused - activity is managed by RefreshActiveState
		// so turning LocalEnabled off never wipes sources given to other players.
		protected override void OnEnable()
		{
			base.OnEnable(); }
		protected override void OnDisable() { }

		// Intercept kill attempts: kick instead of murder
		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdCheckMurder))]
		class KickerKillPatch
		{
			static bool Prefix(PlayerControl __instance, PlayerControl target)
			{
				if (!Instance.KickerRole || __instance != PlayerControl.LocalPlayer) return true;
				if (target == null || target.Data == null) return false;

				// Don't try to kick the host
				if (target.OwnerId == AmongUsClient.Instance.HostId)
				{
					Bane.notifications.Send("Kicker", "That's the host, you can't kick them!", 5);
					__instance.SetKillTimer(0f);
					return false;
				}

				Bane.Log.LogInfo($"Kicker: kill button used on {target.Data.PlayerName}, kicking instead");

				// Save their position - teleport only after they disconnect (feels like a real kill)
				Vector2 killPos = target.transform.position;
				Instance.pendingKickerTeleports[target.OwnerId] = killPos;

				Utilities.KickPlayer(target);

				// Keep kill timer at 0 (no cooldown)
				__instance.SetKillTimer(0f);
				return false; // block the real murder
			}
		}

		static void ForceKickerRevealText(IntroCutscene intro)
		{
			try
			{
				// Team screen (big "Crewmate"/"Impostor" title before the role card)
				if (intro.TeamTitle != null)
				{
					intro.TeamTitle.text = "Impostor";
					intro.TeamTitle.color = Palette.ImpostorRed;
				}
				if (intro.BackgroundBar != null)
					intro.BackgroundBar.material.color = Palette.ImpostorRed;
				if (intro.ImpostorText != null)
				{
					intro.ImpostorText.gameObject.SetActive(true);
					intro.ImpostorText.text = "Sabotage and kick everyone.";
					intro.ImpostorText.color = Palette.ImpostorRed;
				}

				// Role card
				if (intro.YouAreText != null)
				{
					intro.YouAreText.text = "You are a";
					intro.YouAreText.color = Palette.ImpostorRed;
				}
				if (intro.RoleText != null)
				{
					intro.RoleText.text = "Kicker";
					intro.RoleText.color = Palette.ImpostorRed;
				}
				if (intro.RoleBlurbText != null)
				{
					intro.RoleBlurbText.text = "Kill button kicks players instead of murdering them.";
					intro.RoleBlurbText.color = Palette.ImpostorRed;
				}
			}
			catch (System.Exception e)
			{
				Bane.Log.LogWarning($"Kicker role reveal update failed: {e.Message}");
			}
		}

		// ShowRole is an IEnumerator - postfix fires before texts are written.
		// Start a short coroutine that keeps overriding the texts after the game sets them.
		[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.ShowRole))]
		class KickerRoleRevealPatch
		{
			static void Postfix(IntroCutscene __instance)
			{
				if (!Instance.KickerRole) return;
				__instance.StartCoroutine(OverrideRoleReveal(__instance).WrapToIl2Cpp());
			}

			static IEnumerator OverrideRoleReveal(IntroCutscene intro)
			{
				// Wait a couple frames for the vanilla text assignment inside the coroutine
				yield return null;
				yield return null;

				float end = Time.time + 8f;
				while (intro != null && Time.time < end)
				{
					ForceKickerRevealText(intro);
					yield return null;
				}
			}
		}

		// Also force Impostor team screen if we're kicker (BeginImpostor path)
		[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.BeginImpostor))]
		class KickerBeginImpostorPatch
		{
			static void Postfix(IntroCutscene __instance)
			{
				if (!Instance.KickerRole) return;
				ForceKickerRevealText(__instance);
			}
		}

		[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.BeginCrewmate))]
		class KickerBeginCrewmatePatch
		{
			// If server made us crewmate but Kicker is on, still try to restyle after
			static void Postfix(IntroCutscene __instance)
			{
				if (!Instance.KickerRole) return;
				__instance.StartCoroutine(OverrideRoleReveal(__instance).WrapToIl2Cpp());
			}

			static IEnumerator OverrideRoleReveal(IntroCutscene intro)
			{
				yield return null;
				yield return null;
				float end = Time.time + 8f;
				while (intro != null && Time.time < end)
				{
					ForceKickerRevealText(intro);
					yield return null;
				}
			}
		}

		// Also block the actual MurderPlayer path just in case
		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcMurderPlayer))]
		class KickerMurderBlock
		{
			static bool Prefix(PlayerControl __instance, PlayerControl target)
			{
				if (!Instance.KickerRole || __instance != PlayerControl.LocalPlayer) return true;
				return false;
			}
		}

		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.SetKillTimer))]
		class KickerNoCooldown
		{
			static void Prefix(PlayerControl __instance, ref float time)
			{
				if (!Instance.KickerRole || __instance != PlayerControl.LocalPlayer) return;
				time = 0f;
			}
		}
	}
}
