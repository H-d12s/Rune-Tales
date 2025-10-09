using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stores player team data between encounters so levels, HP, stats and equipped attacks persist.
/// Works with ExperienceSystem and BattleManager.
/// </summary>
public class PersistentPlayerData : MonoBehaviour
{
    public static PersistentPlayerData Instance { get; private set; }

    [System.Serializable]
    public class PlayerRecord
    {
        public CharacterData data;   // reference to CharacterData (asset)
        public string characterName;
        public int level;
        public int currentHP;
        public int maxHP;
        public int attack;
        public int defense;
        public int speed;

        // NEW: store equipped attacks by name for persistence
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

    /// <summary>
    /// Saves all current player stats and HP after a battle or XP gain.
    /// </summary>
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

    /// <summary>
    /// Saves/updates a single player's runtime data.
    /// </summary>
    public void UpdateFromRuntime(CharacterRuntime runtime)
    {
        if (runtime == null || runtime.baseData == null)
            return;

        string key = runtime.baseData.characterName;

        if (!playerRecords.ContainsKey(key))
            playerRecords[key] = new PlayerRecord();

        var record = playerRecords[key];
        record.data = runtime.baseData;
        record.characterName = runtime.baseData.characterName;
        record.level = runtime.currentLevel;
        record.currentHP = Mathf.Clamp(runtime.currentHP, 0, runtime.runtimeHP);
        record.maxHP = runtime.runtimeHP;
        record.attack = runtime.runtimeAttack;
        record.defense = runtime.runtimeDefense;
        record.speed = runtime.runtimeSpeed;

        // Save equipped attacks as names (so we can later find the AttackData by name)
        record.equippedAttackNames = new List<string>();
        if (runtime.equippedAttacks != null)
        {
            foreach (var atk in runtime.equippedAttacks)
            {
                if (atk != null && !string.IsNullOrEmpty(atk.attackName))
                    record.equippedAttackNames.Add(atk.attackName);
            }
        }

        playerRecords[key] = record;

        Debug.Log($"💾 Saved {key}: L{record.level}, HP {record.currentHP}/{record.maxHP}, Moves: {record.equippedAttackNames.Count}");
    }

    /// <summary>
    /// Applies saved player data (level, HP, stats, equipped attacks) to a CharacterRuntime instance.
    /// Call this after the CharacterRuntime has been constructed (i.e., after InitializeCharacter).
    /// </summary>
    public void ApplyToRuntime(CharacterRuntime runtime)
    {
        if (runtime == null || runtime.baseData == null)
            return;

        string key = runtime.baseData.characterName;

        if (playerRecords.ContainsKey(key))
        {
            var record = playerRecords[key];
            runtime.currentLevel = Mathf.Max(1, record.level);
            runtime.runtimeHP = Mathf.Max(1, record.maxHP);
            runtime.runtimeAttack = Mathf.Max(0, record.attack);
            runtime.runtimeDefense = Mathf.Max(0, record.defense);
            runtime.runtimeSpeed = Mathf.Max(0, record.speed);

            // restore currentHP (but clamp to new runtimeHP)
            runtime.currentHP = Mathf.Clamp(record.currentHP, 0, runtime.runtimeHP);

            // Restore equipped attacks by name. We try current level first, then search other levels.
            if (runtime.equippedAttacks == null)
                runtime.equippedAttacks = new List<AttackData>();
            runtime.equippedAttacks.Clear();

            foreach (string atkName in record.equippedAttackNames)
            {
                var found = FindAttackByName(runtime.baseData, atkName, runtime.currentLevel);
                if (found != null)
                    runtime.equippedAttacks.Add(found);
                else
                    Debug.LogWarning($"⚠️ Could not find attack '{atkName}' for {runtime.baseData.characterName} when restoring equipped attacks.");
            }

            Debug.Log($"♻️ Restored {key} - Level {runtime.currentLevel}, HP {runtime.currentHP}/{runtime.runtimeHP}, Moves: {runtime.equippedAttacks.Count}");
        }
        else
        {
            // First-time entry → create a record from runtime
            UpdateFromRuntime(runtime);
        }
    }

    /// <summary>
    /// Returns a list of CharacterRuntime objects built from saved PlayerRecords.
    /// These runtimes are ready to be used to spawn controllers (they are instantiated here
    /// so EncounterManager can read baseData references and create the playerTeam list).
    /// </summary>
    public List<CharacterRuntime> GetAllPlayerRuntimes()
    {
        var list = new List<CharacterRuntime>();

        foreach (var kv in playerRecords)
        {
            var rec = kv.Value;
            if (rec == null || rec.data == null) continue;

            // Construct a runtime using saved level (this will initialize base stats)
            var runtime = new CharacterRuntime(rec.data, Mathf.Max(1, rec.level));

            // Override runtime numeric values with saved ones
            runtime.runtimeHP = Mathf.Max(1, rec.maxHP);
            runtime.runtimeAttack = Mathf.Max(0, rec.attack);
            runtime.runtimeDefense = Mathf.Max(0, rec.defense);
            runtime.runtimeSpeed = Mathf.Max(0, rec.speed);

            // restore current HP clipped into valid range
            runtime.currentHP = Mathf.Clamp(rec.currentHP, 0, runtime.runtimeHP);

            // Restore equipped attacks (matching by name)
            runtime.equippedAttacks = runtime.equippedAttacks ?? new List<AttackData>();
            runtime.equippedAttacks.Clear();
            foreach (string atkName in rec.equippedAttackNames)
            {
                var found = FindAttackByName(rec.data, atkName, rec.level);
                if (found != null)
                    runtime.equippedAttacks.Add(found);
                else
                    Debug.LogWarning($"⚠️ Could not find attack '{atkName}' for {rec.data.characterName} when building runtime list.");
            }

            list.Add(runtime);
        }

        return list;
    }

    /// <summary>
    /// Find an AttackData by name from a CharacterData's available attacks.
    /// Tries preferredLevel first, then searches other levels up to a reasonable cap.
    /// </summary>
    private AttackData FindAttackByName(CharacterData data, string attackName, int preferredLevel)
    {
        if (data == null || string.IsNullOrEmpty(attackName))
            return null;

        // Try preferred level first
        var avail = data.GetAvailableAttacks(Mathf.Max(1, preferredLevel));
        if (avail != null)
        {
            var hit = avail.Find(a => a != null && a.attackName == attackName);
            if (hit != null) return hit;
        }

        // Search levels 1..maxSearchLevel (some reasonable cap)
        int maxSearchLevel = Mathf.Max(preferredLevel, 30); // 30 is a reasonable default cap
        for (int lvl = 1; lvl <= maxSearchLevel; lvl++)
        {
            avail = data.GetAvailableAttacks(lvl);
            if (avail == null) continue;
            var hit = avail.Find(a => a != null && a.attackName == attackName);
            if (hit != null) return hit;
        }

        // Not found
        return null;
    }

    /// <summary>
    /// Clear saved data (debug/new game).
    /// </summary>
    public void ClearAll()
    {
        playerRecords.Clear();
        Debug.Log("🧹 Cleared all persistent player data!");
    }
}
