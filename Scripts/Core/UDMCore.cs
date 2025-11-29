  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Threading.Tasks;
  using UDM_Core.Scripts.Configs;
  using UDM_Core.Scripts.Attributes;
  using UDM_Core.Scripts.Module;
  using UnityEngine;
  using UnityEngine.AddressableAssets;
  using UnityEngine.ResourceManagement.AsyncOperations;

  #if UNITY_EDITOR
  using UnityEditor;
  #endif

  namespace UDM_Core.Scripts.Core
  {
    public static class UDMCore
    {
      public static UDMCoreConfig Config => _config;
      public static bool IsInitialized { get; private set; }
      public static event Action Initialized;
      
      private static readonly Dictionary<Type, IUDMModule> ModulesInstances = new();
      private static readonly List<Type> RegisteredTypes = new();
      private static UDMCoreConfig _config;
      
      // AssetsReferences
      private static AssetReferenceT<UDMCoreConfig> _configReference = new(ConfigAddress);
      
      // Constants
      private const string ConfigAddress = "UDMCore Config";
      
#if UNITY_EDITOR
      [InitializeOnLoadMethod]
      private static async void EditorInit()
      {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
          return;
        
        Debug.Log("UDMCore Editor Init");
        await Constructor();
        
        IsInitialized = true; 
        Initialized?.Invoke();
      }
#endif
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
      private static async void RuntimeInit()
      {
        Debug.Log("UDMCore Runtime Init");
        await Constructor();
        InstantiateModules();
        
        IsInitialized = true;
        Initialized?.Invoke();
      }
      
      private static async Task Constructor()
      {
        IsInitialized = false;
        await LoadConfig();
        ApplyConfigData();

        RegisterAllModules();
        
        UpdateConfigWithModules();
      }

      public static IUDMModule GetModule(Type type) => 
        ModulesInstances.GetValueOrDefault(type);

      public static TModule GetModule<TModule>() where TModule : class, IUDMModule =>
        ModulesInstances.GetValueOrDefault(typeof(TModule)) as TModule;
      
      private static async Task LoadConfig()
      {
        var handle = _configReference.LoadAssetAsync();
        _config = await handle.Task;
        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
          _config = handle.Result;
          Debug.Log($"Data: {_config.RegisteredModuleTypeNames.Count}");
          Debug.Log("[UDM_Core] Конфиг успешно загружен через Addressables");
        }
        else
        {
          Debug.LogError($"[UDM_Core] Не удалось загрузить конфиг по адресу: {ConfigAddress}\nОшибка: {handle.OperationException}");
        }
      }
      
      private static Type GetTypeFromAllAssemblies(string typeName)
      {
        // Если тип в Assembly-CSharp
        var type = Type.GetType(typeName);
        if (type != null)
          return type;

        // Тип в разных сборках
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
          type = assembly.GetType(typeName);
          if (type != null)
            return type;
        }

        return null;
      }
      
      private static void ApplyConfigData()
      {
        if (_config == null || _config.RegisteredModuleTypeNames == null) return;

        RegisteredTypes.Clear();

        foreach (var typeName in _config.RegisteredModuleTypeNames)
        {
          var type = GetTypeFromAllAssemblies(typeName);
          if (type != null && ValidateModuleType(type))
          {
            RegisteredTypes.Add(type);
          }
          else if (type != null && !ValidateModuleType(type))
          {
            Debug.LogWarning($"[UDM_Core] Тип {typeName} не прошел валидацию и теперь не регистрируется!");
          }
          else
          {
            Debug.LogWarning($"[UDM_Core] Не удалось восстановить тип: {typeName}!\nВозможно положение типа было изменено или он был удален из проекта.");
          }
        }

        Debug.Log($"[UDM_Core] Из конфига восстановлено модулей: {RegisteredTypes.Count}");
      }
      
      private static void UpdateConfigWithModules()
      {
        if (_config == null) return;

        _config.RegisteredModuleTypeNames = RegisteredTypes
          .Select(t => t.FullName)
          .ToList();

#if UNITY_EDITOR
        EditorUtility.SetDirty(_config);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[UDM_Core] Конфиг сохранён на диск: {_config.RegisteredModuleTypeNames.Count} модулей");
#endif
      }
      
      private static void RegisterAllModules()
      {
        int count = 0;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
          // Пропуск системных и Unity-сборок для оптимизации
          if (assembly.FullName.StartsWith("Unity"))
            continue;
          if (assembly.FullName.StartsWith("System"))
            continue;
          if (assembly.FullName.StartsWith("mscorlib"))
            continue;

          foreach (var type in assembly.GetTypes())
          {
            if (!ValidateModuleType(type)) continue;

            // Добавляем только если тип не был подтянут из конфига
            if (!RegisteredTypes.Contains(type))
            {
              RegisteredTypes.Add(type);
              Debug.Log($"[UDM_Core] Модуль {type.Name} зарегистрирован!");
              count++;
            }
          }
        }

        Debug.Log($"[UDM_Core] Всего зарегистрировано новых модулей: {count}");
      }

      /// <summary>
      /// 
      /// </summary>
      /// <param name="type"></param>
      /// <returns>True if type correct</returns>
      private static bool ValidateModuleType(Type type)
      {
        // Должен быть помечен атрибутом
        if (!Attribute.IsDefined(type, typeof(UDMModuleAttribute)))
          return false;

        // Должен реализовывать IUDMModule
        if (!typeof(IUDMModule).IsAssignableFrom(type))
        {
          Debug.LogWarning($"[UDM_Core] Тип {type.FullName} имеет [UDMModule], но не реализует IUDMModule!");
          return false;
        }

        // Не абстрактный и не интерфейс
        if (type.IsAbstract || type.IsInterface)
        {
          Debug.LogWarning($"[UDM_Core] Тип {type.FullName} абстрактный или является интерфейсом!");
          return false;
        }
        
        return true;
      }
      
      public static void InstantiateModules()
      {
        var list = new List<IUDMModule>();
        foreach (var type in RegisteredTypes)
          try
          {
            var module = Activator.CreateInstance(type) as IUDMModule;
            ModulesInstances.Add(type, module);
            Debug.Log($"[UDM_Core] {type} {module.GetType()}");
          }
          catch (Exception ex)
          {
            Debug.LogError($"[UDM_Core] Не удалось создать {type.Name}: {ex}");
          }
        Debug.Log("[UDM_Core] Modules initialized!");
      }
    }
  }