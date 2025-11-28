using System;
using System.Collections.Generic;
using System.Linq;
using UDM_Core.Scripts.Attributes;
using UDM_Core.Scripts.Module;
using UnityEditor;
using UnityEngine;

namespace UDM_Core.Scripts.Core
{
  public static class UDM_Core
  {
    public static readonly Dictionary<Type, IUDMModule> ModulesInstances = new();
    private static readonly List<Type> _registeredTypes = new();

#if UNITY_EDITOR
    [InitializeOnLoadMethod]
    private static void EditorInit() =>
      Constructor();
#else
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]

    private static void RuntimeInit() =>
      Constructor();
#endif
    
    private static void Constructor()
    {
      ValidateRegisteredModules();
      RegisterAllModules();
    }
    
    private static void RegisterAllModules()
    {
      foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
      {
        // Пропускаем системные и Unity-ассамблеи для скорости
        if (assembly.FullName.StartsWith("Unity"))
          continue;
        if (assembly.FullName.StartsWith("System"))
          continue;
        if (assembly.FullName.StartsWith("mscorlib"))
          continue;

        foreach (var type in assembly.GetTypes())
        {
          if (!ValidateModuleType(type)) continue;

          // Добавляем только если ещё нет
          if (!_registeredTypes.Contains(type))
            _registeredTypes.Add(type);
          
          Debug.Log($"[UDM_Core] Модуль {type.Name} зарегистрирован!");
        }
      }

      Debug.Log($"[UDM_Core] Зарегистрировано модулей: {_registeredTypes.Count}");
      // Опционально: вывести имена
      // foreach (var t in _registeredTypes) Debug.Log("  → " + t.Name);
    }

    private static bool ValidateRegisteredModules()
    {
      if (_registeredTypes != null && _registeredTypes.Count > 0)
        foreach (var type in _registeredTypes.ToList())
          if (!ValidateModuleType(type))
          {
            Debug.Log($"[UDM_Core] Модуль {type.Name} теперь не регистрируется!");
            _registeredTypes.Remove(type);
            return false;
          }

      return true;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="type"></param>
    /// <returns>True if type correct</returns>
    private static bool ValidateModuleType(Type type)
    {
      // 1. Должен быть помечен атрибутом
      if (!Attribute.IsDefined(type, typeof(UDMModuleAttribute)))
        return false;

      // 2. Должен реализовывать IUDMModule
      if (!typeof(IUDMModule).IsAssignableFrom(type))
      {
        Debug.LogWarning($"[UDM_Core] Тип {type.FullName} имеет [UDMModule], но не реализует IUDMModule!");
        return false;
      }

      // 3. Не абстрактный и не интерфейс
      if (type.IsAbstract || type.IsInterface)
        return false;
      
      return true;
    }

    // Удобный метод создания экземпляров
    public static List<IUDMModule> InstantiateModules()
    {
      var list = new List<IUDMModule>();
      foreach (var type in _registeredTypes)
        try
        {
          if (Activator.CreateInstance(type) is IUDMModule module)
            list.Add(module);
        }
        catch (Exception ex)
        {
          Debug.LogError($"[UDM_Core] Не удалось создать {type.Name}: {ex}");
        }

      return list;
    }
  }
}