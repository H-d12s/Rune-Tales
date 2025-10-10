using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stores player team data between encounters so levels, HP, stats and equipped attacks persist.
/// Works with ExperienceSystem, BattleManager, and RecruitmentManager.
/// </summary>
public class PersistentPlayerData : MonoBehaviour
{
    public static PersistentPlayerData Instance { get; private set; }

    [System.Serializable]
    public class PlayerRecord
    {
        public CharacterData data;
        public string characterName;
        public int level;
        public int currentHP;
        public int maxHP;
        public int attack;
        public int defense;
        public int speed;
        public List<string> equippedAttackNames = new List<string>();
    }

    private Dictionary<string, PlayerRecord> playerRecords = new Dictionary<string, PlayerRecord>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // =====================================================
    // 💾 CORE SAVE / LOAD
    // =====================================================
    public void SaveAllPlayers(List<CharacterBattleController> playerControllers)
    {
        if (playerControllers == null) return;
        foreach (var controller in playerControllers)
        {
            if (controller == null) continue;
            var runtime = controller.GetRuntimeCharacter();
            UpdateFromRuntime(runtime);
        }
    }

    public void UpdateFromRuntime(CharacterRuntime runtime)
    {
        if (runtime == null || runtime.baseData == null) return;
        string key = runtime.baseData.characterName;

        if (!playerRecords.ContainsKey(key))
            playerRecords[key] = new PlayerRecord();

        var record = playerRecords[key];
        record.data = runtime.baseData;
        record.characterName = key;
        record.level = runtime.currentLevel;
        record.currentHP = Mathf.Clamp(runtime.currentHP, 0, runtime.runtimeHP);
        record.maxHP = runtime.runtimeHP;
        record.attack = runtime.runtimeAttack;
        record.defense = runtime.runtimeDefense;
        record.speed = runtime.runtimeSpeed;

        // Save attacks
        record.equippedAttackNames.Clear();
        if (runtime.equippedAttacks != null)
        {
            foreach (var atk in runtime.equippedAttacks)
            {
                if (atk != null && !string.IsNullOrEmpty(atk.attackName))
                    record.equippedAttackNames.Add(atk.attackName);
            }
        }

        playerRecords[key] = record;
        Debug.Log($"💾 Saved {key}: L{record.level}, HP {record.currentHP}/{record.maxHP}");
    }

    public void ApplyToRuntime(CharacterRuntime runtime)
    {
        if (runtime == null || runtime.baseData == null) return;
        string key = runtime.baseData.characterName;

        if (playerRecords.TryGetValue(key, out var record))
        {
            runtime.currentLevel = Mathf.Max(1, record.level);
            runtime.runtimeHP = Mathf.Max(1, record.maxHP);
            runtime.runtimeAttack = Mathf.Max(0, record.attack);
            runtime.runtimeDefense = Mathf.Max(0, record.defense);
            runtime.runtimeSpeed = Mathf.Max(0, record.speed);
            runtime.currentHP = Mathf.Clamp(record.currentHP, 0, runtime.runtimeHP);

            // Restore attacks
            runtime.equippedAttacks = new List<AttackData>();
            foreach (string atkName in record.equippedAttackNames)
            {
                var atk = FindAttackByName(runtime.baseData, atkName, record.level);
                if (atk != null) runtime.equippedAttacks.Add(atk);
            }

            Debug.Log($"♻️ Restored {key}: Level {record.level}, HP {record.currentHP}/{record.maxHP}");
        }
        else
        {
            UpdateFromRuntime(runtime);
        }
    }

    public List<CharacterRuntime> GetAllPlayerRuntimes()
    {
        var list = new List<CharacterRuntime>();

        foreach (var kv in playerRecords)
        {
            var rec = kv.Value;
            if (rec?.data == null) continue;

            var runtime = new CharacterRuntime(rec.data, Mathf.Max(1, rec.level))
            {
                runtimeHP = Mathf.Max(1, rec.maxHP),
                runtimeAttack = rec.attack,
                runtimeDefense = rec.defense,
                runtimeSpeed = rec.speed,
                currentHP = Mathf.Clamp(rec.currentHP, 0, rec.maxHP),
                equippedAttacks = new List<AttackData>()
            };

            foreach (string atkName in rec.equippedAttackNames)
            {
                var atk = FindAttackByName(rec.data, atkName, rec.level);
                if (atk != null) runtime.equippedAttacks.Add(atk);
            }

            list.Add(runtime);
        }

        return list;
    }

    private AttackData FindAttackByName(CharacterData data, string attackName, int preferredLevel)
    {
        if (data == null || string.IsNullOrEmpty(attackName)) return null;

        var attacks = data.GetAvailableAttacks(preferredLevel);
        if (attacks != null)
        {
            var hit = attacks.Find(a => a != null && a.attackName == attackName);
            if (hit != null) return hit;
        }

        // Fallback: scan other levels
        for (int lvl = 1; lvl <= 30; lvl++)
        {
            attacks = data.GetAvailableAttacks(lvl);
            if (attacks == null) continue;
            var hit = attacks.Find(a => a != null && a.attackName == attackName);
            if (hit != null) return hit;
        }

        return null;
    }

    // =====================================================
    // 🧩 RECRUITMENT HELPERS
    // =====================================================
    public void AddRecruitedCharacter(CharacterRuntime recruit)
    {
        if (recruit == null || recruit.baseData == null)
        {
            Debug.LogError("❌ Tried to add null recruit to PersistentPlayerData!");
            return;
        }

        string name = recruit.baseData.characterName;
        if (playerRecords.ContainsKey(name))
        {
            Debug.Log($"⚠️ {name} already in team, skipping recruit add.");
            return;
        }

        // ✅ If full team, wait for replacement prompt handled by RecruitmentManager
        if (playerRecords.Count >= 3)
        {
            Debug.Log("⚠️ Team full, need replacement via RecruitmentManager.");
            return;
        }

        UpdateFromRuntime(recruit);
        Debug.Log($"🤝 {name} successfully added to player team!");
    }

    public void RemoveCharacter(string characterName)
    {
        if (string.IsNullOrEmpty(characterName)) return;
        if (playerRecords.Remove(characterName))
            Debug.Log($"👋 Removed {characterName} from team.");
        else
            Debug.LogWarning($"⚠️ Tried to remove {characterName}, but not found in team data.");
    }

    public void ReplaceCharacter(string oldName, CharacterRuntime newRecruit)
    {
        if (string.IsNullOrEmpty(oldName) || newRecruit == null || newRecruit.baseData == null)
        {
            Debug.LogError("❌ Invalid replacement request.");
            return;
        }

        if (!playerRecords.ContainsKey(oldName))
        {
            Debug.LogWarning($"⚠️ Tried to replace {oldName}, but not found. Adding recruit instead.");
        }
        else
        {
            playerRecords.Remove(oldName);
            Debug.Log($"👋 Removed {oldName} from team.");
        }

        AddRecruitedCharacter(newRecruit);
        Debug.Log($"🌟 {newRecruit.baseData.characterName} joined in place of {oldName}!");
    }

    public int GetPlayerCount() => playerRecords.Count;
    public List<string> GetPlayerNames() => new List<string>(playerRecords.Keys);

    public List<CharacterData> GetActiveTeam()
    {
        List<CharacterData> dataList = new List<CharacterData>();
        foreach (var rec in playerRecords.Values)
            if (rec?.data != null)
                dataList.Add(rec.data);
        return dataList;
    }

    public void ClearAll()
    {
        playerRecords.Clear();
        Debug.Log("🧹 Cleared all persistent player data!");
    }
}
