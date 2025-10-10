using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq; // ✅ Required for ToList()

public class RecruitmentManager : MonoBehaviour
{
    private CharacterRuntime recruit;
    private int attemptsLeft = 3;
    private float persuasionChance = 5f;
    private bool recruitmentActive = false;

    // === ENTRY POINT ===
    public void StartRecruitment(CharacterRuntime newRecruit)
    {
        recruit = newRecruit;
        attemptsLeft = 3;
        persuasionChance = 0f;
        recruitmentActive = true;

        Debug.Log($"🧠 Persuasion started with {recruit.baseData.characterName}. You have {attemptsLeft} attempts.");
        ShowRecruitmentPrompt();
    }

    private void ShowRecruitmentPrompt()
    {
        Debug.Log($"Press P to attempt persuasion. Attempts left: {attemptsLeft}");
    }

    // === PLAYER INPUT ===
    private void Update()
    {
        if (!recruitmentActive) return;

        if (Input.GetKeyDown(KeyCode.P))
        {
            AttemptPersuasion(Random.Range(10, 50)); // 🔧 Replace with actual persuasion formula later
        }
    }

    // === PERSUASION LOGIC ===
    private void AttemptPersuasion(int damageDone)
    {
        if (attemptsLeft <= 0)
        {
            Debug.Log($"❌ {recruit.baseData.characterName} lost interest and left...");
            recruitmentActive = false;
            return;
        }

        attemptsLeft--;
        persuasionChance += damageDone * 0.5f; // Scale chance by damage done
        float successRoll = Random.Range(0f, 100f);

        Debug.Log($"🎯 Persuasion chance: {persuasionChance:F1}%, roll: {successRoll:F1}");

        if (successRoll < persuasionChance)
        {
            Debug.Log($"✅ {recruit.baseData.characterName} has joined your team!");
            AddRecruitToTeam();
            recruitmentActive = false;
        }
        else
        {
            Debug.Log($"💬 {recruit.baseData.characterName} remains unconvinced. Attempts left: {attemptsLeft}");
        }
    }

    // === ADD RECRUIT ===
    private void AddRecruitToTeam()
    {
        var playerTeam = PersistentPlayerData.Instance.GetAllPlayerRuntimes();

        if (playerTeam.Count < 3)
        {
            // ✅ Team has space — add directly
            PersistentPlayerData.Instance.AddRecruitedCharacter(recruit);

            var playerControllers = FindObjectsOfType<CharacterBattleController>().ToList();
            PersistentPlayerData.Instance.SaveAllPlayers(playerControllers);

            Debug.Log($"🎉 {recruit.baseData.characterName} joined your team!");
        }
        else
        {
            // 🧠 Team full — choose who to replace
            Debug.Log("⚠️ Team full! Press 1, 2, or 3 to choose a member to replace.");
            StartCoroutine(WaitForReplacementChoice(playerTeam));
        }
    }

    // === REPLACEMENT SELECTION ===
    private IEnumerator WaitForReplacementChoice(List<CharacterRuntime> team)
    {
        bool replaced = false;
        float waitTime = 15f; // allow 15 seconds max
        float elapsed = 0f;

        while (!replaced && elapsed < waitTime)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
                replaced = ReplaceMember(team[0]);
            else if (Input.GetKeyDown(KeyCode.Alpha2))
                replaced = ReplaceMember(team[1]);
            else if (Input.GetKeyDown(KeyCode.Alpha3))
                replaced = ReplaceMember(team[2]);

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!replaced)
        {
            Debug.Log("⌛ Replacement timed out — recruitment cancelled.");
        }
    }

    // === HANDLE REPLACEMENT ===
    private bool ReplaceMember(CharacterRuntime oldMember)
    {
        Debug.Log($"👋 {oldMember.baseData.characterName} leaves the team. {recruit.baseData.characterName} joins!");

        // ✅ Replace in persistent data
        PersistentPlayerData.Instance.ReplaceCharacter(oldMember.baseData.characterName, recruit);

        // ✅ Save immediately to prevent data loss
        var playerControllers = FindObjectsOfType<CharacterBattleController>().ToList();
        PersistentPlayerData.Instance.SaveAllPlayers(playerControllers);

        // ✅ Refresh visuals if battle is active
        var battleManager = FindObjectOfType<BattleManager>();
        if (battleManager != null)
        {
            battleManager.RefreshPlayerVisuals();
            Debug.Log("🔄 Refreshed player visuals after recruitment.");
        }

        return true;
    }
}
