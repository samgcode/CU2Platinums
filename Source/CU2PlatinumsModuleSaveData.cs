using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.CU2Platinums;

public class CU2PlatinumsModuleSaveData : EverestModuleSaveData
{
  public Dictionary<string, Vector2> SpawnPositions { get; set; } = new Dictionary<string, Vector2>();
}
