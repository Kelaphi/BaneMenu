using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;

namespace BaneMenu.routines
{
	public abstract class Routine
	{
		public readonly string name;

		protected bool _enabled = false;
		public virtual bool Enabled
		{
			get { return _enabled; }
			set
			{
				if(value == _enabled) return;
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

		protected Routine(string name)
		{
			this.name = name;
		}

		public abstract void Run();

		protected virtual void OnEnable() { }
		protected virtual void OnDisable() { }

		public Dictionary<string, JsonElement> GetConfigData()
		{
			Dictionary<string, JsonElement> configData = new Dictionary<string, JsonElement>();
			Type type = GetType();

			foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
			{
				if (!property.CanRead || !property.CanWrite) continue;
				if (property.GetIndexParameters().Length > 0) continue;
				if (property.PropertyType == typeof(Type) || typeof(Delegate).IsAssignableFrom(property.PropertyType)) continue;
				try
				{
					configData[property.Name] = JsonSerializer.SerializeToElement(property.GetValue(this, null));
				}
				catch { }
			}

			foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
			{
				if (field.IsInitOnly || field.IsLiteral) continue;
				if (field.FieldType == typeof(Type) || typeof(Delegate).IsAssignableFrom(field.FieldType)) continue;
				if (configData.ContainsKey(field.Name)) continue;
				try
				{
					configData[field.Name] = JsonSerializer.SerializeToElement(field.GetValue(this));
				}
				catch { }
			}

			return configData;
		}

		public void LoadConfigData(Dictionary<string, JsonElement> configData)
		{
			if (configData == null) return;
			Type type = GetType();

			foreach ((string propertyName, JsonElement propertyValue) in configData)
			{
				PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
				if (property != null && property.CanWrite && property.GetIndexParameters().Length == 0)
				{
					try { property.SetValue(this, propertyValue.Deserialize(property.PropertyType)); }
					catch (Exception ex) { Bane.Log.LogWarning($"Failed to load property {propertyName} for routine {name}: {ex.Message}"); }
					continue;
				}

				FieldInfo field = type.GetField(propertyName, BindingFlags.Instance | BindingFlags.Public);
				if (field != null && !field.IsInitOnly)
				{
					try { field.SetValue(this, propertyValue.Deserialize(field.FieldType)); }
					catch (Exception ex) { Bane.Log.LogWarning($"Failed to load field {propertyName} for routine {name}: {ex.Message}"); }
					continue;
				}

				Bane.Log.LogWarning($"Config has setting {propertyName} for routine {name} when this routine has no such setting");
			}
		}
	}
}