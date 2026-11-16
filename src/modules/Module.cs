using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace BaneMenu.modules
{
	internal abstract class Module
	{
		public readonly string name;
		public readonly Type[] incompatibilities;

		protected bool _enabled = false;
		public virtual bool Enabled
		{
			get { return _enabled; }
			set
			{
				if(_enabled == value) return;
				_enabled = value;

				if(value)
				{
					OnEnable();
				}
				else
				{
					OnDisable();
				}
				Bane.config?.MarkDirty();
			}
		}

		protected Module(string name, Type[] incompatibilities = null)
		{
			this.name = name;
			this.incompatibilities = incompatibilities ?? Array.Empty<Type>();
		}

		protected virtual void OnEnable()
		{
			// Handle module incompatibilities
			if(Bane.modules != null)
			{
				foreach(Module module in Bane.modules.moduleList)
				{
					if(!incompatibilities.Contains(module.GetType()) || !module.Enabled) continue;

					Bane.notifications.Send(name, $"The module {module.name} is incompatible with this module, so it has been disabled.", 5);
					module.Enabled = false;
				}
			}
		}

		protected virtual void OnDisable() { }

		public Dictionary<string, JsonElement> GetConfigData()
		{
			Dictionary<string, JsonElement> configData = new Dictionary<string, JsonElement>();

			Type type = GetType();

			foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
			{
				if (!property.CanRead || !property.CanWrite) continue;
				if (property.GetIndexParameters().Length > 0) continue;
				// Skip non-serializable / runtime-only
				if (property.PropertyType == typeof(Type) || typeof(Delegate).IsAssignableFrom(property.PropertyType)) continue;
				try
				{
					configData[property.Name] = JsonSerializer.SerializeToElement(property.GetValue(this, null));
				}
				catch { /* skip non-serializable */ }
			}

			foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
			{
				if (field.IsInitOnly || field.IsLiteral) continue;
				if (field.FieldType == typeof(Type) || typeof(Delegate).IsAssignableFrom(field.FieldType)) continue;
				// Avoid duplicating auto-property backing if any (none expected for public fields)
				if (configData.ContainsKey(field.Name)) continue;
				try
				{
					configData[field.Name] = JsonSerializer.SerializeToElement(field.GetValue(this));
				}
				catch { /* skip non-serializable */ }
			}

			return configData;
		}

		public void LoadConfigData(Dictionary<string, JsonElement> configData)
		{
			if (configData == null) return;
			Type type = GetType();

			foreach ((string propertyName, JsonElement propertyValue) in configData)
			{
				// Enabled: set without short-circuit, fire OnEnable/OnDisable once
				if (propertyName == "Enabled")
				{
					try
					{
						bool want = propertyValue.Deserialize<bool>();
						if (_enabled != want)
							Enabled = want;
					}
					catch (Exception ex)
					{
						Bane.Log.LogWarning($"Failed to load Enabled for module {name}: {ex.Message}");
					}
					continue;
				}

				PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
				if (property != null && property.CanWrite && property.GetIndexParameters().Length == 0)
				{
					try
					{
						property.SetValue(this, propertyValue.Deserialize(property.PropertyType));
					}
					catch (Exception ex)
					{
						Bane.Log.LogWarning($"Failed to load property {propertyName} for module {name}: {ex.Message}");
					}
					continue;
				}

				FieldInfo field = type.GetField(propertyName, BindingFlags.Instance | BindingFlags.Public);
				if (field != null && !field.IsInitOnly)
				{
					try
					{
						field.SetValue(this, propertyValue.Deserialize(field.FieldType));
					}
					catch (Exception ex)
					{
						Bane.Log.LogWarning($"Failed to load field {propertyName} for module {name}: {ex.Message}");
					}
					continue;
				}

				Bane.Log.LogWarning($"Config has setting {propertyName} for module {name} when this module has no such setting");
			}
		}
	}
}
