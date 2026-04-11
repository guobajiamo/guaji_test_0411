using Godot;

namespace Test00_0410.Systems;

/// <summary>
/// 合成系统。
/// 未来会与事务辅助器配合，确保合成过程要么全成功，要么全回滚。
/// </summary>
public partial class CraftingSystem : Node
{
    public bool TryCraft(string recipeId)
    {
        // 当前先保留接口。
        return false;
    }
}
