using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Test00_0410.Core.SaveLoad;

/// <summary>
/// 存档迁移执行器。
/// 当版本变更时，负责把旧存档升级成新结构。
/// </summary>
public class MigrationRunner
{
    private readonly List<MigrationStep> _steps = new();

    public void RegisterMigration(MigrationStep step)
    {
        _steps.Add(step);
    }

    public SaveData RunMigrations(SaveData saveData)
    {
        SemanticVersion currentVersion = SemanticVersion.Parse(saveData.Metadata.SaveVersion);
        SemanticVersion targetVersion = SemanticVersion.Current;

        GD.Print($"[MigrationRunner] 检查存档版本: {currentVersion}");

        while (currentVersion < targetVersion)
        {
            MigrationStep? nextStep = _steps.FirstOrDefault(step => step.FromVersion.Equals(currentVersion));
            if (nextStep == null)
            {
                GD.PushWarning($"[MigrationRunner] 没有找到从 {currentVersion} 开始的迁移步骤，保留原存档结构。");
                break;
            }

            nextStep.Apply(saveData);
            saveData.Metadata.SaveVersion = nextStep.ToVersion.ToString();
            currentVersion = nextStep.ToVersion;
        }

        return saveData;
    }
}

/// <summary>
/// 单个迁移步骤。
/// 明确表达“从哪个版本迁到哪个版本”，避免只有 from 没有 to。
/// </summary>
public sealed class MigrationStep
{
    public MigrationStep(SemanticVersion fromVersion, SemanticVersion toVersion, Action<SaveData> apply, string description = "")
    {
        FromVersion = fromVersion;
        ToVersion = toVersion;
        Apply = apply;
        Description = description;
    }

    public SemanticVersion FromVersion { get; }

    public SemanticVersion ToVersion { get; }

    public Action<SaveData> Apply { get; }

    public string Description { get; }
}
