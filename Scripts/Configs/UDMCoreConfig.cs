using System;
using System.Collections.Generic;
using UDM_Core.Scripts.Module;
using UnityEngine;

namespace UDM_Core.Scripts.Configs
{
  [CreateAssetMenu(fileName = "UDMCore Config", menuName = "UDM Core/UDMCore Config", order = 0)]
  public class UDMCoreConfig : ScriptableObject
  {
    public List<string> RegisteredModuleTypeNames = new();
  }
}