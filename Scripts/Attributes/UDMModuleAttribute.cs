using System;

namespace UDM_Core.Scripts.Attributes
{
  [AttributeUsage(AttributeTargets.Class,
    Inherited = false,
    AllowMultiple = false)]
  public sealed class UDMModuleAttribute : Attribute
  {
  }
}