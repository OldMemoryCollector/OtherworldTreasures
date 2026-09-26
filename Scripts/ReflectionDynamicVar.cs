using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace OtherworldTreasures.Scripts;

/// <summary>
/// 从 Owner 对象的同名属性动态读取值的 DynamicVar。
/// BaseValue 始终从属性实时获取，不缓存。
/// </summary>
public class ReflectionDynamicVar : DynamicVar
{
    private readonly string _propertyName;
    private readonly decimal _fallback;

    public ReflectionDynamicVar(string propertyName, decimal fallback = 0m) : base(propertyName, fallback)
    {
        _propertyName = propertyName;
        _fallback = fallback;
    }

    protected override decimal GetBaseValueForIConvertible()
    {
        if (_owner == null) return _fallback;
        var prop = _owner.GetType().GetProperty(_propertyName);
        if (prop == null) return _fallback;
        var value = prop.GetValue(_owner);
        if (value == null) return _fallback;
        try
        {
            return Convert.ToDecimal(value);
        }
        catch
        {
            return _fallback;
        }
    }
}
