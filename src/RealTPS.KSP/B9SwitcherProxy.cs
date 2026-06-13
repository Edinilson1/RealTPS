using System;
using System.Collections;
using System.Reflection;

namespace RealTPS
{
    /// <summary>
    /// Late-bound access to one <c>ModuleB9PartSwitch</c> on a part. RealTPS never links against B9PartSwitch;
    /// every member is looked up by name and every call is guarded, so that a missing or changed B9 leaves
    /// RealTPS in its detached mode instead of throwing inside the part lifecycle.
    /// Members used (B9PartSwitch 2.21): field <c>moduleID</c>, field <c>subtypes</c>, property
    /// <c>CurrentSubtypeName</c>, methods <c>SwitchSubtype(string)</c> and <c>IsManagedResource(string)</c>,
    /// subtype property <c>Name</c>, and the stock <c>IPartMassModifier</c> interface.
    /// </summary>
    internal sealed class B9SwitcherProxy
    {
        public const string ModuleClassName = "ModuleB9PartSwitch";

        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private readonly PartModule module;
        private readonly Type type;

        private B9SwitcherProxy(PartModule module)
        {
            this.module = module;
            type = module.GetType();
        }

        public PartModule Module
        {
            get { return module; }
        }

        /// <summary>Finds the switcher with this <c>moduleID</c>; with an empty id, the first switcher on the part.</summary>
        public static B9SwitcherProxy Find(Part part, string moduleId)
        {
            if (part == null || part.Modules == null)
            {
                return null;
            }
            for (int i = 0; i < part.Modules.Count; i++)
            {
                PartModule candidate = part.Modules[i];
                if (candidate == null || candidate.ClassName != ModuleClassName)
                {
                    continue;
                }
                var proxy = new B9SwitcherProxy(candidate);
                if (string.IsNullOrEmpty(moduleId) || string.Equals(proxy.ModuleId, moduleId, StringComparison.Ordinal))
                {
                    return proxy;
                }
            }
            return null;
        }

        public string ModuleId
        {
            get
            {
                try
                {
                    FieldInfo field = type.GetField("moduleID", Flags);
                    return field != null ? field.GetValue(module) as string : null;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        /// <summary>Name of the active subtype, or null when it cannot be read.</summary>
        public string CurrentSubtypeName
        {
            get
            {
                try
                {
                    PropertyInfo property = type.GetProperty("CurrentSubtypeName", Flags);
                    return property != null ? property.GetValue(module, null) as string : null;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        public bool HasSubtype(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }
            try
            {
                FieldInfo field = type.GetField("subtypes", Flags);
                var list = field != null ? field.GetValue(module) as IEnumerable : null;
                if (list == null)
                {
                    return false;
                }
                foreach (object subtype in list)
                {
                    if (subtype == null)
                    {
                        continue;
                    }
                    PropertyInfo nameProperty = subtype.GetType().GetProperty("Name", Flags);
                    if (nameProperty != null && string.Equals(nameProperty.GetValue(subtype, null) as string, name, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }
            return false;
        }

        /// <summary>Asks B9 to activate a subtype. Returns true only when B9 then reports that subtype as current.</summary>
        public bool SwitchSubtype(string name)
        {
            try
            {
                MethodInfo method = type.GetMethod("SwitchSubtype", Flags, null, new[] { typeof(string) }, null);
                if (method == null)
                {
                    return false;
                }
                method.Invoke(module, new object[] { name });
                return string.Equals(CurrentSubtypeName, name, StringComparison.Ordinal);
            }
            catch (Exception e)
            {
                RtpsLog.Error("B9PartSwitch refused subtype '" + name + "': " + (e.InnerException ?? e).Message);
                return false;
            }
        }

        /// <summary>True when any subtype of this switcher declares the resource.</summary>
        public bool IsManagedResource(string resourceName)
        {
            try
            {
                MethodInfo method = type.GetMethod("IsManagedResource", Flags, null, new[] { typeof(string) }, null);
                return method != null && (bool)method.Invoke(module, new object[] { resourceName });
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Dry mass the switcher currently contributes to the part, t (resource mass excluded).</summary>
        public double DryMass
        {
            get
            {
                try
                {
                    var modifier = module as IPartMassModifier;
                    return modifier != null ? modifier.GetModuleMass(0f, ModifierStagingSituation.CURRENT) : 0.0;
                }
                catch (Exception)
                {
                    return 0.0;
                }
            }
        }
    }
}
