using System;

[Serializable]
public sealed class DiaryProgressionSettings
{
    public int InitialFragments = 3;
    public int FragmentsPerLevel = 1;
    public int MaximumFragments = 12;

    public int RequiredForLevel(int level)
    {
        if (level < 1) throw new ArgumentOutOfRangeException("level");
        if (InitialFragments < 1 || FragmentsPerLevel < 1 || MaximumFragments < InitialFragments || MaximumFragments > 12)
            throw new ArgumentException("Diary progression requires 1..12 fragments and a positive per-level increment.");
        return (int)Math.Min(MaximumFragments, InitialFragments + (long)(level - 1) * FragmentsPerLevel);
    }
}

public sealed class DiaryProgress
{
    readonly bool[] collected;
    public int Revision { get; private set; }
    public int RequiredCount { get { return collected.Length; } }
    public int CollectedCount { get; private set; }
    public bool IsComplete { get { return CollectedCount == RequiredCount; } }

    public DiaryProgress(int required, int revision)
    {
        if (required < 1 || required > 12) throw new ArgumentOutOfRangeException("required");
        if (revision < 0) throw new ArgumentOutOfRangeException("revision");
        collected = new bool[required]; Revision = revision;
    }

    public bool TryCollect(int id, int revision)
    {
        if (revision != Revision || id < 0 || id >= collected.Length || collected[id]) return false;
        collected[id] = true; CollectedCount++; return true;
    }
}
