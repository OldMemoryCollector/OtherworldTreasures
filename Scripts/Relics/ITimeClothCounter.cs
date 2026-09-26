namespace OtherworldTreasures.Scripts.Relics;

// 可被【时光布】回溯"计数"的遗物（如剩余使用次数、剩余次数角标）。
// 计数均为 static，所以按类型即可存取；不需要实例身份。
public interface ITimeClothCounter
{
    // 时光布可回溯的计数
    int TimeClothCounter { get; set; }

    // 计数变化后刷新角标 UI
    void RefreshTimeClothCounterUi();
}
