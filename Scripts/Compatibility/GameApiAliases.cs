// Match the native extra-turn power without referencing a removed type at load time.
#if STS2_113
global using ExtraTurnEffectPower = MegaCrit.Sts2.Core.Models.Powers.ExtraTurnPower;
#else
global using ExtraTurnEffectPower = MegaCrit.Sts2.Core.Models.Powers.AmbergrisPower;
#endif
