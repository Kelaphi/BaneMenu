using BepInEx;
using BaneMenu.anticheat;
using BaneMenu.modules.host;
using BaneMenu.modules.misc;
using BaneMenu.modules.troll;
using BaneMenu.ui;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace BaneMenu.modules
{
	internal class ConfigManager
	{
		public readonly string CONFIG_PATH = Path.Combine(Paths.ConfigPath, "BaneMenu");

		public readonly List<string> configList = new List<string>();
		public string currentConfig = "BaneMenu";

		private bool _dirty;
		private bool _loading;
		private float _dirtyAt;
		private const float AutosaveDelay = 0.5f;

		public class ConfigData
		{
			public MainUI.MainUIConfig Menu { get; set; }
			public Dictionary<string, Dictionary<string, JsonElement>> Modules { get; set; }
			public Dictionary<string, Dictionary<string, JsonElement>> Routines { get; set; }
			public Anticheat.AnticheatConfigData Anticheat { get; set; }
			/// <summary>Static / non-module menu settings (DevUnlock, HostDummies, etc.).</summary>
			public Dictionary<string, JsonElement> Globals { get; set; }
		}

		public void Initialize()
		{
			if(!Directory.Exists(CONFIG_PATH))
			{
				Bane.Log.LogInfo("No config folder was found, creating...");
				Directory.CreateDirectory(CONFIG_PATH);

				configList.Add(currentConfig);
				// First run: every module stays off (constructors no longer force-enable)
				SaveConfig(currentConfig);
				return;
			}

			string[] configFiles = Directory.GetFiles(CONFIG_PATH, "*.json");
			Bane.Log.LogInfo($"Discovered {configFiles.Length} config files");

			foreach(string file in configFiles)
			{
				configList.Add(Path.GetFileNameWithoutExtension(file));
			}

			// Prefer loading existing default config; only create if the file is truly missing
			string defaultPath = GetConfigPath(currentConfig);
			if (!configList.Contains(currentConfig))
				configList.Add(currentConfig);

			if (!File.Exists(defaultPath))
			{
				// First run only - save current (all-off) defaults
				SaveConfig(currentConfig);
				return;
			}

			// Load the default config
			LoadConfig(currentConfig);
		}

		public string GetConfigPath(string configName)
		{
			return Path.Combine(CONFIG_PATH, configName + ".json");
		}

		public void LoadConfig(string configName)
		{
			_loading = true;
			try { LoadConfigInner(configName); }
			finally { _loading = false; }
		}

		void LoadConfigInner(string configName)
		{
			string configLocation = GetConfigPath(configName);
			if(!File.Exists(configLocation))
			{
				Bane.Log.LogWarning($"Tried to load config {configName} when no such config exists");
				// Let's just carry on with our current config
				return;
			}

			string configString = File.ReadAllText(configLocation);

			ConfigData configData;
			try
			{
				configData = JsonSerializer.Deserialize<ConfigData>(configString);
			}
			catch
			{
				Bane.Log.LogError($"Failed to load config at {configLocation}");
				return;
			}

			if (configData == null)
			{
				Bane.Log.LogError("Config deserialize returned null");
				return;
			}
			try { if (configData.Menu != null) Bane.mainUI.LoadConfigData(configData.Menu); }
			catch (System.Exception ex) { Bane.Log.LogWarning("Menu config: " + ex.Message); }
			try { if (configData.Modules != null) Bane.modules.LoadConfigData(configData.Modules); }
			catch (System.Exception ex) { Bane.Log.LogWarning("Modules config: " + ex.Message); }
			try { if (configData.Routines != null) Bane.routines.LoadConfigData(configData.Routines); }
			catch (System.Exception ex) { Bane.Log.LogWarning("Routines config: " + ex.Message); }
			try { if (configData.Anticheat != null) Anticheat.LoadConfigData(configData.Anticheat); }
			catch (System.Exception ex) { Bane.Log.LogWarning("Anticheat config: " + ex.Message); }
			try { LoadGlobals(configData.Globals); }
			catch (System.Exception ex) { Bane.Log.LogWarning("Globals config: " + ex.Message); }

			currentConfig = configName;
			Bane.Log.LogInfo($"Loaded config {configName}");
		}

		public void SaveConfig(string configName)
		{
			string configLocation = GetConfigPath(configName);

			ConfigData configData = new ConfigData();
			configData.Menu = Bane.mainUI.GetConfigData();
			configData.Modules = Bane.modules.GetConfigData();
			configData.Routines = Bane.routines.GetConfigData();
			configData.Anticheat = Anticheat.GetConfigData();
			configData.Globals = CollectGlobals();

			JsonSerializerOptions serializerOptions = new JsonSerializerOptions();
			serializerOptions.WriteIndented = true;

			string configString = JsonSerializer.Serialize(configData, serializerOptions);
			File.WriteAllText(configLocation, configString);

			Bane.Log.LogInfo($"Config {configName} has been saved to {configLocation}");
		}

		static Dictionary<string, JsonElement> CollectGlobals()
		{
			var g = new Dictionary<string, JsonElement>();
			try
			{
				g["DevUnlock"] = JsonSerializer.SerializeToElement(DevUnlock.Enabled);
				g["QuickRejoin.Delay"] = JsonSerializer.SerializeToElement(misc.QuickRejoin.Delay);
				g["HydralumPresence.Enabled"] = JsonSerializer.SerializeToElement(misc.HydralumPresence.Enabled);
				g["ModMenuCheck.Enabled"] = JsonSerializer.SerializeToElement(anticheat.ModMenuCheck.Enabled);
				g["ModMenuCheck.DetectSicko"] = JsonSerializer.SerializeToElement(anticheat.ModMenuCheck.DetectSicko);
				g["HostDummies.controlAll"] = JsonSerializer.SerializeToElement(HostDummies.controlAll);
				g["HostDummies.controlDummies"] = JsonSerializer.SerializeToElement(HostDummies.controlDummies);
				g["HostDummies.moveSpeed"] = JsonSerializer.SerializeToElement(HostDummies.moveSpeed);
				g["HostDummies.optionsBypass"] = JsonSerializer.SerializeToElement(HostDummies.optionsBypass);
				g["Nullstorm.RpcSpam"] = JsonSerializer.SerializeToElement(Nullstorm.RpcSpam);
			}
			catch (System.Exception ex)
			{
				Bane.Log.LogWarning("CollectGlobals: " + ex.Message);
			}
			return g;
		}

		static void LoadGlobals(Dictionary<string, JsonElement> g)
		{
			if (g == null) return;
			try
			{
				if (g.TryGetValue("DevUnlock", out var du)) DevUnlock.Enabled = du.Deserialize<bool>();
				if (g.TryGetValue("QuickRejoin.Delay", out var qd)) misc.QuickRejoin.Delay = qd.Deserialize<float>();
				if (g.TryGetValue("HydralumPresence.Enabled", out var hp)) misc.HydralumPresence.Enabled = hp.Deserialize<bool>();
				if (g.TryGetValue("ModMenuCheck.Enabled", out var mm)) anticheat.ModMenuCheck.Enabled = mm.Deserialize<bool>();
				if (g.TryGetValue("ModMenuCheck.DetectSicko", out var msSicko)) anticheat.ModMenuCheck.DetectSicko = msSicko.Deserialize<bool>();
				if (g.TryGetValue("HostDummies.controlAll", out var ca)) HostDummies.controlAll = ca.Deserialize<bool>();
				if (g.TryGetValue("HostDummies.controlDummies", out var cd)) HostDummies.controlDummies = cd.Deserialize<bool>();
				if (g.TryGetValue("HostDummies.moveSpeed", out var msSpeed)) HostDummies.moveSpeed = msSpeed.Deserialize<float>();
				if (g.TryGetValue("HostDummies.optionsBypass", out var ob)) HostDummies.optionsBypass = ob.Deserialize<bool>();
				if (g.TryGetValue("Nullstorm.RpcSpam", out var ns)) Nullstorm.RpcSpam = ns.Deserialize<bool>();
			}
			catch (System.Exception ex)
			{
				Bane.Log.LogWarning("LoadGlobals: " + ex.Message);
			}
		}

		public string GetUnusedConfigName()
		{
			HashSet<string> configHashList = configList.ToHashSet();
			string match = null;

			// https://stackoverflow.com/a/27289807
			char[] digits = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };
			string baseConfigName = currentConfig.TrimEnd(digits).Trim();

			for(int i = 1; i < 255; i++)
			{
				string configName = baseConfigName + " " + i;
				if(configHashList.Contains(configName)) continue;

				match = configName;
				break;
			}

			return match;
		}

		public void CreateNewConfig(string configName)
		{
			// First save our old config
			SaveConfig(currentConfig);

			// Then create our new config
			configList.Add(configName);
			SaveConfig(configName);
			currentConfig = configName;
		}

		public void MarkDirty()
		{
			if (_loading) return;
			_dirty = true;
			_dirtyAt = UnityEngine.Time.realtimeSinceStartup;
		}

		public void TickAutosave()
		{
			if (!_dirty) return;
			if (UnityEngine.Time.realtimeSinceStartup - _dirtyAt < AutosaveDelay) return;
			_dirty = false;
			try { SaveConfig(currentConfig); }
			catch (System.Exception ex) { Bane.Log.LogWarning("Autosave failed: " + ex.Message); }
		}
	}
}