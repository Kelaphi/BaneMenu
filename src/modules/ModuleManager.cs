using BaneMenu.modules.host;
using BaneMenu.modules.misc;
using BaneMenu.modules.protections;
using BaneMenu.modules.roles;
using BaneMenu.modules.self;
using BaneMenu.modules.spoofer;
using BaneMenu.modules.troll;
using BaneMenu.modules.visuals;
using System;
using System.Collections.Generic;
using System.Text.Json;
using UnityEngine;

namespace BaneMenu.modules
{
	internal class ModuleManager : MonoBehaviour
	{
		// Host
		public static AssignRoles assignRoles = new AssignRoles();
		public static AlwaysAllowStart alwaysAllowStart = new AlwaysAllowStart();
		public static BotDetector botDetector = new BotDetector();
		public static DisableKillButton disableKillButton = new DisableKillButton();
		public static BanMidGame banMidGame = new BanMidGame();
		public static BlockLowLevels blockLowLevels = new BlockLowLevels();
		public static DisableGameEnd disableGameEnd = new DisableGameEnd();
		public static DisableMeetings disableMeetings = new DisableMeetings();
		public static DisableVentClean disableVentClean = new DisableVentClean();
		public static DisableSporeTriggers disableSporeTriggers = new DisableSporeTriggers();
		public static DisableZipline disableZipline = new DisableZipline();
		public static FakeShapeshiftBubble fakeShapeshiftBubble = new FakeShapeshiftBubble();
		public static FlipSkeld flipSkeld = new FlipSkeld();
		public static SmallLobby smallLobby = new SmallLobby();
		public static TempBanAll tempBanAll = new TempBanAll();
		public static VoteImmune voteImmune = new VoteImmune();

		// Misc
		public static ChatLogger chatLogger = new ChatLogger();
		public static Whisper whisper = new Whisper();
		public static MatchmakerFilters matchmakerFilters = new MatchmakerFilters();
		public static ChatMultiply chatMultiply = new ChatMultiply();
		public static AnticheatWatch anticheatWatch = new AnticheatWatch();
		public static RpcLogger rpcLogger = new RpcLogger();
		public static TestBanPlayerInfoDelay testBanPlayerInfoDelay = new TestBanPlayerInfoDelay();
		public static RpcDrop rpcDrop = new RpcDrop();

		// Protections
		public static AntiCrash antiCrash = new AntiCrash();
		public static AntiKick antiKick = new AntiKick();
		public static AntiOverload antiOverload = new AntiOverload();
		public static BlockServerTeleports blockServerTeleports = new BlockServerTeleports();
		public static BlockUnauthorizedUpdates blockUnauthorizedUpdates = new BlockUnauthorizedUpdates();
		public static IgnoreHackingBan ignoreHackingBan = new IgnoreHackingBan();
		public static BypassDisconnectPenalty bypassDisconnectPenalty = new BypassDisconnectPenalty();
		public static BypassShapeshiftRatelimits bypassShapeshiftRatelimits = new BypassShapeshiftRatelimits();
		public static ForceDTLs forceDtls = new ForceDTLs();

		// Roles
		public static MoveInVents moveInVents = new MoveInVents();
		public static NoKillChecks noKillChecks = new NoKillChecks();
		public static NoSabotageCooldown noSabotageCooldown = new NoSabotageCooldown();
		public static NoShapeshiftAnimation noShapeshiftAnimation = new NoShapeshiftAnimation();
		public static NoPhantomAnimation noPhantomAnimation = new NoPhantomAnimation();
		public static UnlockSabotageButton unlockSabotageButton = new UnlockSabotageButton();
		public static VentAsCrewmate ventAsCrewmate = new VentAsCrewmate();

		// Self
		public static AlwaysShowTaskAnimations alwaysShowTaskAnimations = new AlwaysShowTaskAnimations();
		public static HideYourself hideYourself = new HideYourself();
		public static Immortality immortality = new Immortality();
		public static NoLadderCooldown noLadderCooldown = new NoLadderCooldown();
		public static NoZiplineCooldown noZiplineCooldown = new NoZiplineCooldown();
		public static SpeedModifier speedModifier = new SpeedModifier();
		public static NanPosition nanPosition = new NanPosition();
		public static ClickTeleport clickTeleport = new ClickTeleport();
		public static AutoHostOnGameEnd autoHostOnGameEnd = new AutoHostOnGameEnd();
		public static DoTasksAsImpostor doTasksAsImpostor = new DoTasksAsImpostor();
		public static UnlimitedMeetings unlimitedMeetings = new UnlimitedMeetings();
		public static UpdateStatsFreeplay updateStatsFreeplay = new UpdateStatsFreeplay();

		// Spoofer
		public static SpoofDevice spoofDevice = new SpoofDevice();
		public static SpoofLevel spoofLevel = new SpoofLevel();
		public static SpoofVersion spoofVersion = new SpoofVersion();

		// Troll
		public static AutoExposeImpostors autoExposeImpostors = new AutoExposeImpostors();
		public static Blackout blackout = new Blackout();
		public static AutoReportBodies autoReportBodies = new AutoReportBodies();
		public static AntiStartGame antiStartGame = new AntiStartGame();
		public static LobbyVoteKickLoop lobbyVoteKickLoop = new LobbyVoteKickLoop();
		public static ForceHost forceHost = new ForceHost();
		public static CrashLobby crashLobby = new CrashLobby();
		public static MeetingSpam meetingSpam = new MeetingSpam();
		public static DisableCameras disableCameras = new DisableCameras();
		public static DisableCloseDoors disableCloseDoors = new DisableCloseDoors();
		public static DisableSabotages disableSabotages = new DisableSabotages();
		public static DisableVents disableVents = new DisableVents();

		// Visual
		public static AccurateDisconnectReason accurateDisconnectReason = new AccurateDisconnectReason();
		public static AlwaysVisibleChat alwaysVisibleChat = new AlwaysVisibleChat();
		public static Fullbright fullbright = new Fullbright();
		public static NoSeekerAnimation noSeekerAnimation = new NoSeekerAnimation();
		public static ShowAnonymousVotes showAnonymousVotes = new ShowAnonymousVotes();
		public static RealtimeVotes realtimeVotes = new RealtimeVotes();
		public static ShowGhostMessages showGhostMessages = new ShowGhostMessages();
		public static ShowGhosts showGhosts = new ShowGhosts();
		public static ShowProtections showProtections = new ShowProtections();
		public static SkipShhhAnimation skipShhhAnimation = new SkipShhhAnimation();
		public static Zoom zoom = new Zoom();
		public static RevealRoles revealRoles = new RevealRoles();
		public static ModDetection modDetection = new ModDetection();
		public static SpectatePlayer spectatePlayer = new SpectatePlayer();

		public readonly Module[] moduleList;

		public ModuleManager()
		{
			moduleList = [
				assignRoles,
				alwaysAllowStart,
				banMidGame,
				disableKillButton,
				blockLowLevels,
				disableGameEnd,
				disableMeetings,
				disableVentClean,
				disableSporeTriggers,
				disableZipline,
				fakeShapeshiftBubble,
				flipSkeld,
				smallLobby,
				tempBanAll,
				voteImmune,

				chatLogger,
				whisper,
				chatMultiply,
				anticheatWatch,
				rpcLogger,
				testBanPlayerInfoDelay,
				rpcDrop,

				antiCrash,
				antiKick,
				antiOverload,
				blockServerTeleports,
				blockUnauthorizedUpdates,
				bypassDisconnectPenalty,
				ignoreHackingBan,
				bypassShapeshiftRatelimits,
				forceDtls,

				moveInVents,
				noKillChecks,
				noSabotageCooldown,
				noShapeshiftAnimation,
				noPhantomAnimation,
				unlockSabotageButton,
				ventAsCrewmate,

				alwaysShowTaskAnimations,
				doTasksAsImpostor,
				hideYourself,
				immortality,
				matchmakerFilters,
				noLadderCooldown,
				noZiplineCooldown,
				speedModifier,
				nanPosition,
				clickTeleport,
				autoHostOnGameEnd,
				unlimitedMeetings,
				updateStatsFreeplay,

				spoofDevice,
				spoofLevel,
				spoofVersion,

				autoExposeImpostors,
				blackout,
				autoReportBodies,
				antiStartGame,
				lobbyVoteKickLoop,
				botDetector,
				forceHost,
				crashLobby,
				meetingSpam,
				disableCameras,
				disableCloseDoors,
				disableSabotages,
				disableVents,

				accurateDisconnectReason,
				alwaysVisibleChat,
				fullbright,
				noSeekerAnimation,
				showAnonymousVotes,
				realtimeVotes,
				showGhostMessages,
				showGhosts,
				showProtections,
				skipShhhAnimation,
				zoom,
				revealRoles,
				modDetection,
				spectatePlayer
			];
		}

		// Return a dictionary of each module with its name, and another dictionary with names and values of each property
		// Role assignment is intentionally excluded - those are session-specific.
		public Dictionary<string, Dictionary<string, JsonElement>> GetConfigData()
		{
			Dictionary<string, Dictionary<string, JsonElement>> moduleConfig = new Dictionary<string, Dictionary<string, JsonElement>>();

			foreach(Module module in moduleList)
			{
				if (module is host.AssignRoles) continue;
				moduleConfig.Add(module.name, module.GetConfigData());
			}

			return moduleConfig;
		}

		public void LoadConfigData(Dictionary<string, Dictionary<string, JsonElement>> moduleConfig)
		{
			if (moduleConfig == null) return;
			foreach((string moduleName, Dictionary<string, JsonElement> configData) in moduleConfig)
			{
				if (moduleName == "AssignRoles") continue;
				int moduleIndex = Array.FindIndex(moduleList, r => r.name == moduleName);
				if(moduleIndex == -1)
				{
					Bane.Log.LogWarning($"Config has entry for module {moduleName} when there is no such module");
					continue;
				}

				Module module = moduleList[moduleIndex];
				module.LoadConfigData(configData);
			}
		}

		// No idea where else to put this
		public void Update()
		{
			// If PlayerControl::Data isn't null, then we know the player has fully loaded into the game
			if(PlayerControl.LocalPlayer == null || PlayerControl.LocalPlayer.Data == null) return;

			if(unlockSabotageButton.SabotageAsCrewmate) HudManager.Instance.SabotageButton.gameObject.SetActive(true);
			if(ventAsCrewmate.Enabled) HudManager.Instance.ImpostorVentButton.gameObject.SetActive(true);


			if(nanPosition.Enabled)
				nanPosition.Tick();
			if(autoHostOnGameEnd.Enabled)
				autoHostOnGameEnd.Tick();
			if(anticheatWatch.Enabled)
				anticheatWatch.Tick();
			if(testBanPlayerInfoDelay.Enabled)
				testBanPlayerInfoDelay.Tick();
			if (disableKillButton != null) disableKillButton.Tick();
			if (meetingSpam != null && meetingSpam.Enabled) meetingSpam.Tick();
			if (hideYourself != null && hideYourself.Enabled) hideYourself.Tick();
			if (botDetector != null && botDetector.Enabled) botDetector.Tick();

			// Hydralum presence (read-only) for Hydralum user tags
			BaneMenu.modules.misc.HydralumPresence.Tick();

			// AuraMenu HostCheats.ControlPlayers (ported)
			BaneMenu.modules.host.HostDummies.ControlPlayers();

			// AuraMenu Nullstorm RpcSpam (fixed Delay 0.04 / SetLevel)
			BaneMenu.modules.troll.Nullstorm.TickRpcSpam();
		}
	}
}
