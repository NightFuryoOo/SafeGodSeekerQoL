using Modding.Utils;
using Osmi.Utils.Tap;
using System.Diagnostics.CodeAnalysis;
using static Mono.Security.X509.X520;

namespace GodhomeQoL;

public static class ModuleManager
{
    private static readonly Lazy<Dictionary<string, Module>> modules = new(() => Assembly
        .GetExecutingAssembly()
        .GetTypesSafely()
        .Filter(type => type.IsSubclassOf(typeof(Module)) && !type.IsAbstract)
        .OrderBy(type => type.FullName)
#if DEBUG
		.Filter(type => {
			if (type.GetConstructor(Type.EmptyTypes) == null) {
				LogError($"Default constructor not found on module type {type.FullName}");
				return false;
			}

			if (!type.IsSealed) {
				LogWarn($"Module type {type.FullName} is not sealed");
			}

			return true;
		})
		.Map(type => {
			try {
				return (Activator.CreateInstance(type) as Module)!;
			} catch {
				LogError($"Failed to initialize module {type.FullName}");
				return null!;
			}
		})
#else
        .Map(type => (Activator.CreateInstance(type) as Module)!)
#endif
        .ToDictionary(module => module.Name)
    );

    internal static Dictionary<string, Module> Modules => modules.Value;

    internal static readonly Dictionary<int, (string suppressor, Module[] modules)> suppressions = [];
    private static int lastSuppressionHandle = 0;

    internal static void Load()
    {
        try
        {
            Modules.Values.ForEach(module => module.Active = true);

        }
        catch (Exception ex)
        {
            Logger.Log(ex.Message);
        }

    }


    internal static void Unload()
    {
        try
        {
            Modules.Values.ForEach(module => module.Active = false);


        }
        catch (Exception ex)
        {
            Logger.Log(ex.Message);
        }

    }

    public static Module GetModule<T>() where T : Module
    {
        try
        {
            _ = TryGetModule(typeof(T).Name, out Module? m);
            return m!;

        }
        catch (Exception ex)
        {
            Logger.Log(ex.Message);
            return null!;
        }

    }

    public static bool TryGetModule(Type type, [NotNullWhen(true)] out Module? module)
    {
        try
        {
            return TryGetModule(type.Name, out module);

        }
        catch (Exception ex)
        {
            Logger.Log(ex.Message);
            module = null;
            return false;
        }

    }

    public static bool TryGetModule(string name, [NotNullWhen(true)] out Module? module)
    {
        try
        {
            module = Modules.TryGetValue(name, out Module? m) ? m : null;
            return module != null;
        }
        catch (Exception ex)
        {
            Logger.Log(ex.Message);
            module = null;
            return false;
        }

    }

    public static bool TryGetLoadedModule<T>([NotNullWhen(true)] out T? module) where T : Module
    {
        try
        {
            bool ret = TryGetLoadedModule(typeof(T).Name, out Module? m);
            module = m as T;
            return ret;
        }
        catch (Exception ex)
        {
            Logger.Log(ex.Message);
            module = null;
            return false;
        }
        
    }

    public static bool TryGetLoadedModule(Type type, [NotNullWhen(true)] out Module? module) =>
        TryGetLoadedModule(type.Name, out module);

    public static bool TryGetLoadedModule(string name, [NotNullWhen(true)] out Module? module)
    {
        try
        {
            module = Modules.TryGetValue(name, out Module? m) && m.Loaded ? m : null;
            return module != null;
        }
        catch (Exception ex)
        {
            Logger.Log(ex.Message);
            module = null;
            return false;
        }
        
    }

    public static bool IsModuleLoaded<T>() where T : Module
    {
        try
        {
            return TryGetLoadedModule<T>(out _);
        }
        catch (Exception ex)
        {
            Logger.Log(ex.Message);
            return false;
        }

    }


    public static bool IsModuleLoaded(Type type)
    {
        try
        {
            return TryGetLoadedModule(type, out _);
        }
        catch (Exception ex)
        {
            Logger.Log(ex.Message);
            return false;
        }

    }


    public static bool IsModuleLoaded(string name)
    {
        try
        {
            return TryGetLoadedModule(name, out _);
        }
        catch (Exception ex)
        {
            Logger.Log(ex.Message);
            return false;
        }

    }
   


    public static int SuppressModules(string suppressor, params Module[] modules)
    {
        try
        {

            int handle = ++lastSuppressionHandle;

            suppressions.Add(handle, (suppressor, modules));

            foreach (Module module in modules)
            {
                module.suppressorMap.Add(handle, suppressor);
                module.UpdateStatus();
            }

            Log(suppressor + " starts to suppress modules " + modules.Map(m => m.Name).Join(", ") + " with handle " + handle);

            return handle;
        }
        catch (Exception ex)
        {
            Logger.Log(ex.Message);
            return 0;
        }
        
    }

    public static int SuppressModule<T>(string suppressor) where T : Module
    {
        try
        {

            return SuppressModules(suppressor, GetModule<T>());
        }
        catch (Exception ex)
        {
            Logger.Log(ex.Message);
            return 0;
        }
    }

    public static int SuppressModules(string suppressor, params string[] modules)
    {
        try
        {

            return SuppressModules(suppressor, modules.Map(name => TryGetModule(name, out Module? m)
            ? m
            : throw new InvalidOperationException("Unknown module " + name)
        ).ToArray());
        }
        catch (Exception ex)
        {
            Logger.Log(ex.Message);
            return 0;
        }
    }
   

    public static void CancelSuppression(int handle)
    {
        try
        {

            if (!suppressions.TryGetValue(handle, out (string suppressor, Module[] modules) suppression))
            {
                LogError("Failed attempt to end unknown suppresion with handle " + handle);
                return;
            }

            _ = suppressions.Remove(handle);
            (string suppressor, Module[] modules) = suppression;

            foreach (Module module in modules)
            {
                _ = module.suppressorMap.Remove(handle);
                module.UpdateStatus();
            }

            Log(suppressor + " end to suppress modules " + modules.Map(m => m.Name).Join(", ") + " with handle " + handle);
        }
        catch (Exception ex)
        {
            Logger.Log(ex.Message);
        }
       
    }
}
