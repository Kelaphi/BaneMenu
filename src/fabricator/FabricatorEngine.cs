using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BaneMenu.fabricator
{
	/// <summary>
	/// Client-side multi-player macros.
	/// Players are fabricated with GameData.AddDummy (real Data + cosmetics) so the game
	/// can drive walk anims / chat bubbles. Nothing is networked (no AmongUsClient.Spawn).
	/// </summary>
	internal static class FabricatorEngine
	{
		public static readonly List<Macro> Library = new List<Macro>();
		public static int selectedMacroIndex;

		public static bool IsRecording { get; private set; }
		private static float recordStartRealtime;
		private static PlayerTrack recordingTrack;
		private static float lastSampleT = -1f;
		private const float SampleInterval = 1f / 30f;

		private class PlaybackInstance
		{
			public PlayerTrack track;
			public float startRealtime;
			public int nextEventIndex;
			public PlayerControl shell;
			public bool finished;
			public float lastX, lastY;
			public bool hasLastPos;
		}

		private static readonly List<PlaybackInstance> active = new List<PlaybackInstance>();
		private static byte nextShellId = 240;
		private static bool eventsHooked;

		// -------------------- Public API --------------------

		public static Macro SelectedMacro
		{
			get
			{
				if (Library.Count == 0) return null;
				selectedMacroIndex = Mathf.Clamp(selectedMacroIndex, 0, Library.Count - 1);
				return Library[selectedMacroIndex];
			}
		}

		public static int ActivePlaybackCount => active.Count;

		public static void CreateNewMacro(string name)
		{
			EnsureEventHooks();
			if (string.IsNullOrWhiteSpace(name)) name = "Macro " + (Library.Count + 1);
			Library.Add(new Macro { name = name.Trim() });
			selectedMacroIndex = Library.Count - 1;
			Bane.notifications.Send("Fabricator", $"Created macro '{name.Trim()}'", 3);
		}

		public static void StartNewPlayerRecording()
		{
			EnsureEventHooks();
			if (PlayerControl.LocalPlayer == null)
			{
				Bane.notifications.Send("Fabricator", "Need to be in a game.", 3);
				return;
			}
			if (IsRecording)
			{
				Bane.notifications.Send("Fabricator", "Already recording.", 2);
				return;
			}
			var macro = SelectedMacro;
			if (macro == null)
			{
				Bane.notifications.Send("Fabricator", "Create / select a macro first.", 3);
				return;
			}

			StopAllPlayback(notify: false);
			int played = 0;
			foreach (var track in macro.players)
			{
				if (track == null || track.samples.Count == 0) continue;
				StartPlaybackTrack(track);
				played++;
			}

			recordingTrack = new PlayerTrack();
			CaptureCosmetics(recordingTrack);
			recordStartRealtime = Time.realtimeSinceStartup;
			lastSampleT = -1f;
			IsRecording = true;

			Bane.notifications.Send("Fabricator",
				played > 0
					? $"Recording player #{macro.players.Count + 1} - {played} ghost(s) playing."
					: $"Recording first player for '{macro.name}'.",
				3);
		}

		public static void StopRecording(bool save)
		{
			if (!IsRecording) return;
			IsRecording = false;

			if (recordingTrack == null)
			{
				Bane.notifications.Send("Fabricator", "Nothing recorded.", 2);
				return;
			}

			recordingTrack.duration = Time.realtimeSinceStartup - recordStartRealtime;
			TrySample(force: true);

			var macro = SelectedMacro;
			if (save && recordingTrack.samples.Count > 0 && macro != null)
			{
				macro.players.Add(recordingTrack);
				Bane.notifications.Send("Fabricator",
					$"Saved player into '{macro.name}' ({recordingTrack.duration:F1}s, events={recordingTrack.events.Count}). Players: {macro.players.Count}",
					4);
			}
			else
			{
				Bane.notifications.Send("Fabricator", "Recording discarded.", 2);
			}

			recordingTrack = null;
		}

		public static void PlayFullMacro()
		{
			EnsureEventHooks();
			var macro = SelectedMacro;
			if (macro == null || macro.players.Count == 0)
			{
				Bane.notifications.Send("Fabricator", "No players in selected macro.", 3);
				return;
			}
			if (PlayerControl.LocalPlayer == null)
			{
				Bane.notifications.Send("Fabricator", "Need to be in a game.", 3);
				return;
			}

			StopAllPlayback(notify: false);
			int n = 0;
			foreach (var track in macro.players)
			{
				if (track == null || track.samples.Count == 0) continue;
				StartPlaybackTrack(track);
				n++;
			}
			Bane.notifications.Send("Fabricator", $"Playing '{macro.name}' ({n} player(s)).", 3);
		}

		public static void StopMacro()
		{
			if (IsRecording) StopRecording(save: false);
			StopAllPlayback(notify: true);
		}

		public static void DeleteSelectedMacro()
		{
			if (Library.Count == 0) return;
			selectedMacroIndex = Mathf.Clamp(selectedMacroIndex, 0, Library.Count - 1);
			string n = Library[selectedMacroIndex].name;
			StopMacro();
			Library.RemoveAt(selectedMacroIndex);
			selectedMacroIndex = Mathf.Clamp(selectedMacroIndex, 0, Math.Max(0, Library.Count - 1));
			Bane.notifications.Send("Fabricator", $"Deleted '{n}'", 2);
		}

		public static void RecordEvent(MacroEventType type, Vector2 pos, byte targetId = 255, string text = null)
		{
			if (!IsRecording || recordingTrack == null) return;
			float t = Time.realtimeSinceStartup - recordStartRealtime;
			recordingTrack.events.Add(new MacroEvent
			{
				t = t,
				type = type,
				x = pos.x,
				y = pos.y,
				targetPlayerId = targetId,
				text = text
			});
		}

		// -------------------- Tick --------------------

		public static void Tick()
		{
			EnsureEventHooks();
			if (IsRecording) TrySample(force: false);

			float now = Time.realtimeSinceStartup;
			for (int i = active.Count - 1; i >= 0; i--)
			{
				var p = active[i];
				if (p.shell == null || p.shell.gameObject == null)
				{
					active.RemoveAt(i);
					continue;
				}

				// Keep fabricated shells out of role assignment lists
				TryRemoveFromAllPlayers(p.shell);

				float t = now - p.startRealtime;
				if (p.track == null || t > p.track.duration)
				{
					DestroyShell(p);
					active.RemoveAt(i);
					continue;
				}

				ApplySample(p, t);

				while (p.nextEventIndex < p.track.events.Count &&
				       p.track.events[p.nextEventIndex].t <= t)
				{
					FireEventLocal(p, p.track.events[p.nextEventIndex]);
					p.nextEventIndex++;
				}
			}
		}

		// -------------------- Fabrication --------------------

		private static void StartPlaybackTrack(PlayerTrack track)
		{
			var shell = FabricatePlayer(track);
			if (shell == null) return;
			active.Add(new PlaybackInstance
			{
				track = track,
				startRealtime = Time.realtimeSinceStartup,
				nextEventIndex = 0,
				shell = shell,
				finished = false,
				hasLastPos = false
			});
		}

		/// <summary>
		/// Build a full local player: prefab + GameData.AddDummy (gives NetworkedPlayerInfo)
		/// without AmongUsClient.Spawn so nothing is sent to the network.
		/// </summary>
		private static PlayerControl FabricatePlayer(PlayerTrack track)
		{
			if (AmongUsClient.Instance == null || AmongUsClient.Instance.PlayerPrefab == null)
				return null;

			Vector3 pos = PlayerControl.LocalPlayer != null
				? PlayerControl.LocalPlayer.transform.position
				: Vector3.zero;

			PlayerControl shell = Object.Instantiate(AmongUsClient.Instance.PlayerPrefab, pos, Quaternion.identity);
			if (shell == null) return null;

			try
			{
				byte id = nextShellId;
				if (nextShellId > 200) nextShellId--;
				else nextShellId = 240;
				shell.PlayerId = id;

				// Real PlayerInfo so cosmetics / chat / anim systems have Data
				try
				{
					if (GameData.Instance != null)
						GameData.Instance.AddPlayerInfo(GameData.Instance.AddDummy(shell));
				}
				catch (Exception e)
				{
					Bane.Log.LogWarning("Fabricator AddDummy: " + e.Message);
				}

				// Do NOT AmongUsClient.Spawn - client-side only

				// Apply saved outfit through normal player APIs (Data exists now)
				string name = string.IsNullOrEmpty(track.recordedName) ? "Macro" : track.recordedName;
				int colorId = track.colorId;
				if (colorId < 0 || colorId >= Palette.PlayerColors.Length) colorId = 0;

				try { shell.SetName(name); } catch { }
				try { shell.SetColor((byte)colorId); } catch { }
				try { shell.SetHat(track.hatId ?? "", colorId); } catch { }
				try { shell.SetSkin(track.skinId ?? "", colorId); } catch { }
				try { shell.SetVisor(track.visorId ?? "", colorId); } catch { }
				try { shell.SetNamePlate(track.namePlateId ?? ""); } catch { }
				try { shell.SetPet(track.petId ?? ""); } catch { }

				try
				{
					if (shell.Data != null)
					{
						shell.Data.PlayerName = name;
						try { shell.Data.IsDead = false; } catch { }
						try { shell.Data.Disconnected = false; } catch { }
					}
				}
				catch { }

				// Physics ON so the game can animate walk from velocity
				try { if (shell.NetTransform != null) shell.NetTransform.enabled = false; } catch { }
				try { if (shell.Collider != null) shell.Collider.enabled = false; } catch { }
				try { if (shell.MyPhysics != null) shell.MyPhysics.enabled = true; } catch { }
				try { shell.Visible = true; } catch { }
				try { if (shell.cosmetics != null) shell.cosmetics.Visible = true; } catch { }

				if (shell.cosmetics?.nameText != null)
				{
					shell.cosmetics.nameText.text = name;
					shell.cosmetics.nameText.gameObject.SetActive(true);
				}

				ApplyPlayerMaterialColor(shell, colorId);
				TryRemoveFromAllPlayers(shell);
			}
			catch (Exception e)
			{
				Bane.Log.LogWarning("FabricatePlayer: " + e.Message);
			}

			return shell;
		}

		private static void ApplyPlayerMaterialColor(PlayerControl shell, int colorId)
		{
			try
			{
				var pmType = Type.GetType("PlayerMaterial, Assembly-CSharp") ?? FindType("PlayerMaterial");
				if (pmType == null) return;
				var setColors = pmType.GetMethod("SetColors", BindingFlags.Public | BindingFlags.Static, null,
					new[] { typeof(int), typeof(Renderer) }, null)
					?? pmType.GetMethod("SetColors", BindingFlags.Public | BindingFlags.Static, null,
						new[] { typeof(int), typeof(SpriteRenderer) }, null);
				if (setColors == null) return;
				var renderers = shell.GetComponentsInChildren<SpriteRenderer>(true);
				if (renderers == null) return;
				for (int i = 0; i < renderers.Length; i++)
				{
					if (renderers[i] == null) continue;
					try { setColors.Invoke(null, new object[] { colorId, renderers[i] }); } catch { }
				}
			}
			catch { }
		}

		private static Type FindType(string simpleName)
		{
			foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
			{
				try
				{
					foreach (var t in asm.GetTypes())
						if (t.Name == simpleName) return t;
				}
				catch { }
			}
			return null;
		}

		// -------------------- Movement (native velocity → walk anim) --------------------

		private static void ApplySample(PlaybackInstance p, float t)
		{
			var shell = p.shell;
			var track = p.track;
			if (shell == null || track == null || track.samples.Count == 0) return;

			MacroSample a = track.samples[0];
			MacroSample b = track.samples[track.samples.Count - 1];
			for (int i = 0; i < track.samples.Count; i++)
			{
				if (track.samples[i].t <= t) a = track.samples[i];
				if (track.samples[i].t >= t) { b = track.samples[i]; break; }
			}

			float u = 0f;
			if (b.t > a.t) u = Mathf.Clamp01((t - a.t) / (b.t - a.t));
			float x = Mathf.Lerp(a.x, b.x, u);
			float y = Mathf.Lerp(a.y, b.y, u);

			float dx = 0f, dy = 0f;
			float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
			if (p.hasLastPos)
			{
				dx = x - p.lastX;
				dy = y - p.lastY;
			}
			p.lastX = x;
			p.lastY = y;
			p.hasLastPos = true;

			Vector2 vel = new Vector2(dx / dt, dy / dt);

			// Position + velocity on the physics body so AU's own walk code runs
			try
			{
				if (shell.MyPhysics != null)
				{
					// Rigidbody2D is usually MyPhysics.body in AU
					var bodyProp = shell.MyPhysics.GetType().GetProperty("body",
						BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
						?? shell.MyPhysics.GetType().GetProperty("Body",
							BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
					var body = bodyProp != null ? bodyProp.GetValue(shell.MyPhysics) as Rigidbody2D : null;
					if (body == null)
						body = shell.GetComponent<Rigidbody2D>();
					if (body != null)
					{
						body.isKinematic = false;
						body.position = new Vector2(x, y);
						body.velocity = vel;
					}
					else
					{
						shell.transform.position = new Vector3(x, y, shell.transform.position.z);
					}
				}
				else
				{
					shell.transform.position = new Vector3(x, y, shell.transform.position.z);
				}
			}
			catch
			{
				shell.transform.position = new Vector3(x, y, shell.transform.position.z);
			}

			// Facing
			bool faceLeft = a.facing == 0;
			if (Mathf.Abs(dx) > 0.0005f) faceLeft = dx < 0f;
			try
			{
				var renderers = shell.GetComponentsInChildren<SpriteRenderer>(true);
				if (renderers != null)
				{
					for (int i = 0; i < renderers.Length; i++)
					{
						var sr = renderers[i];
						if (sr == null) continue;
						string n = sr.gameObject.name ?? "";
						if (n.IndexOf("Name", StringComparison.OrdinalIgnoreCase) >= 0) continue;
						sr.flipX = faceLeft;
					}
				}
			}
			catch { }

			// Vent
			bool inVent = u < 0.5f ? a.inVent : b.inVent;
			try { if (shell.cosmetics != null) shell.cosmetics.Visible = !inVent; } catch { }
		}

		// -------------------- Events / chat --------------------

		private static void FireEventLocal(PlaybackInstance p, MacroEvent e)
		{
			switch (e.type)
			{
				case MacroEventType.Chat:
					ShowMacroChat(p, e.text ?? "");
					break;
				case MacroEventType.Kill:
					try
					{
						if (p.shell != null)
							p.shell.transform.position = new Vector3(e.x, e.y, 0f);
					}
					catch { }
					break;
				case MacroEventType.StartMeeting:
				case MacroEventType.ReportBody:
					Bane.notifications.Send("Fabricator",
						$"[Local Meeting] {p.track?.recordedName}", 3);
					break;
				case MacroEventType.EnterVent:
				case MacroEventType.ExitVent:
					try
					{
						if (p.shell?.cosmetics != null)
							p.shell.cosmetics.Visible = e.type == MacroEventType.ExitVent;
					}
					catch { }
					break;
			}
		}

		private static void ShowMacroChat(PlaybackInstance p, string body)
		{
			string who = p?.track?.recordedName ?? "Macro";
			var shell = p?.shell;

			try
			{
				if (HudManager.Instance == null || HudManager.Instance.Chat == null)
				{
					Bane.notifications.Send("Macro Chat", $"[{who}] {body}", 4);
					return;
				}

				ChatController chat = HudManager.Instance.Chat;
				try { chat.SetVisible(true); } catch { }

				// Best path: fabricated shell has Data → normal AddChat
				if (shell != null && shell.Data != null)
				{
					try
					{
						chat.AddChat(shell, body, false);
						Bane.Log.LogInfo($"Fabricator AddChat(shell): [{who}] {body}");
						return;
					}
					catch (Exception ex)
					{
						Bane.Log.LogWarning("AddChat(shell) failed: " + ex.Message);
					}
				}

				// Bubble path using shell Data or local Data
				ChatBubble bubble = null;
				try
				{
					bubble = chat.GetPooledBubble();
					bubble.transform.SetParent(chat.scroller.Inner);
					bubble.transform.localScale = Vector3.one;
					bubble.SetLeft();

					NetworkedPlayerInfo info = null;
					try { info = shell != null ? shell.Data : null; } catch { }
					if (info == null && PlayerControl.LocalPlayer != null)
						info = PlayerControl.LocalPlayer.Data;

					if (info != null)
					{
						try { bubble.SetCosmetics(info); } catch { }
						try
						{
							chat.SetChatBubbleName(bubble, info, false, false,
								PlayerNameColor.Get(info), null);
						}
						catch
						{
							try { bubble.SetName(who, false, false, Color.white); } catch { }
						}
					}
					else
					{
						try { bubble.SetName(who, false, false, Color.white); } catch { }
					}

					// Force display name to macro name
					try { bubble.SetName(who, false, false, Color.white); } catch { }

					bubble.SetText(body ?? "");
					bubble.AlignChildren();
					chat.AlignAllBubbles();

					try
					{
						if (PlayerControl.LocalPlayer != null)
							chat.chatNotification.SetUp(PlayerControl.LocalPlayer, $"[{who}] {body}");
					}
					catch { }

					if (!chat.IsOpenOrOpening && chat.notificationRoutine == null)
					{
						try { chat.notificationRoutine = chat.StartCoroutine(chat.BounceDot()); } catch { }
					}

					Bane.Log.LogInfo($"Fabricator bubble: [{who}] {body}");
					return;
				}
				catch (Exception ex)
				{
					Bane.Log.LogWarning("Fabricator bubble: " + ex.Message);
					try { if (bubble != null) chat.chatBubblePool.Reclaim(bubble); } catch { }
				}

				// Last resort
				try
				{
					if (PlayerControl.LocalPlayer != null)
						chat.AddChat(PlayerControl.LocalPlayer, $"[{who}] {body}", false);
				}
				catch { }

				Bane.notifications.Send("Macro Chat", $"[{who}] {body}", 4);
			}
			catch (Exception ex)
			{
				Bane.Log.LogWarning("ShowMacroChat: " + ex.Message);
				Bane.notifications.Send("Macro Chat", $"[{who}] {body}", 4);
			}
		}

		// -------------------- Bookkeeping --------------------

		private static void StopAllPlayback(bool notify)
		{
			for (int i = active.Count - 1; i >= 0; i--)
				DestroyShell(active[i]);
			active.Clear();
			if (notify)
				Bane.notifications.Send("Fabricator", "Macro stopped.", 2);
		}

		private static void DestroyShell(PlaybackInstance p)
		{
			if (p == null) return;
			try
			{
				if (p.shell != null)
				{
					TryRemoveFromAllPlayers(p.shell);
					// Remove PlayerInfo if present
					try
					{
						if (GameData.Instance != null && p.shell.Data != null)
							GameData.Instance.RemovePlayer(p.shell.PlayerId);
					}
					catch { }
					if (p.shell.gameObject != null)
						Object.Destroy(p.shell.gameObject);
				}
			}
			catch { }
			p.shell = null;
		}

		private static void TryRemoveFromAllPlayers(PlayerControl shell)
		{
			if (shell == null) return;
			try
			{
				var list = PlayerControl.AllPlayerControls;
				if (list != null && list.Contains(shell))
					list.Remove(shell);
			}
			catch { }
		}

		private static void CaptureCosmetics(PlayerTrack track)
		{
			var lp = PlayerControl.LocalPlayer;
			if (lp == null || lp.Data == null) return;
			try
			{
				track.recordedName = lp.Data.PlayerName ?? "Player";
				var o = lp.Data.DefaultOutfit;
				track.colorId = (byte)Mathf.Clamp(o.ColorId, 0, Math.Max(0, Palette.PlayerColors.Length - 1));
				track.hatId = o.HatId ?? "";
				track.skinId = o.SkinId ?? "";
				track.visorId = o.VisorId ?? "";
				track.namePlateId = o.NamePlateId ?? "";
				track.petId = o.PetId ?? "";
			}
			catch { }
		}

		private static void TrySample(bool force)
		{
			if (recordingTrack == null || PlayerControl.LocalPlayer == null) return;
			float t = Time.realtimeSinceStartup - recordStartRealtime;
			if (!force && t - lastSampleT < SampleInterval) return;
			lastSampleT = t;

			var lp = PlayerControl.LocalPlayer;
			Vector2 pos = lp.transform.position;
			byte facing = 1;
			try
			{
				var sr = lp.GetComponentInChildren<SpriteRenderer>();
				if (sr != null) facing = (byte)(sr.flipX ? 0 : 1);
			}
			catch { }

			bool inVent = false;
			try { inVent = lp.inVent; } catch { }

			recordingTrack.samples.Add(new MacroSample
			{
				t = t, x = pos.x, y = pos.y, facing = facing, inVent = inVent
			});
		}

		/// <summary>
		/// Record chat via EventCoordinator (already patched safely) - no extra Harmony here.
		/// </summary>
		private static void EnsureEventHooks()
		{
			if (eventsHooked) return;
			eventsHooked = true;
			try
			{
				BaneMenu.modules.EventCoordinator.OnPlayerChat += OnPlayerChat;
			}
			catch (Exception e)
			{
				Bane.Log.LogWarning("Fabricator event hook: " + e.Message);
			}
		}

		private static void OnPlayerChat(PlayerControl player, string text)
		{
			if (!IsRecording) return;
			if (player != PlayerControl.LocalPlayer) return;
			if (recordingTrack != null && recordingTrack.events.Count > 0)
			{
				var last = recordingTrack.events[recordingTrack.events.Count - 1];
				if (last.type == MacroEventType.Chat && last.text == text) return;
			}
			Vector2 pos = player != null ? (Vector2)player.transform.position : Vector2.zero;
			RecordEvent(MacroEventType.Chat, pos, 255, text);
		}

		// Only patches that are known-good elsewhere in this project (cleanup only)
		[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.CoStartGame))]
		private static class PatchCoStartGame
		{
			static void Prefix()
			{
				try
				{
					if (IsRecording) StopRecording(save: false);
					StopAllPlayback(notify: false);
				}
				catch { }
			}
		}

		[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.ExitGame))]
		private static class PatchExitGame
		{
			static void Prefix()
			{
				try
				{
					if (IsRecording) StopRecording(save: false);
					StopAllPlayback(notify: false);
				}
				catch { }
			}
		}
	}
}
