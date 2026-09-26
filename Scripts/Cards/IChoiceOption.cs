using System.Threading.Tasks;

namespace OtherworldTreasures.Scripts.Cards;

// 抉择选项卡：在"选择一张牌"界面里被选中后立即结算。
// 复用原版机制（知识恶魔「知识的诅咒」同款）：
//   选项 = 卡牌 → CardSelectCmd.FromChooseACardScreen(...) → 选中的卡执行 OnChosen()
public interface IChoiceOption
{
    Task OnChosen();
}
