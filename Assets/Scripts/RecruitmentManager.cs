using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RecruitmentManager : MonoBehaviour
{
    private CharacterRuntime recruit;
    private int attemptsLeft = 3;
    private float persuasionChance = 5f;
    private bool recruitmentActive = false;

    void Awake()
{
    DontDestroyOnLoad(gameObject);
}

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

    private void Update()
    {
        if (!recruitmentActive) return;

        if (Input.GetKeyDown(KeyCode.P))
        {
            AttemptPersuasion(Random.Range(10, 50)); // Replace with actual damage done if integrated
        }
    }

    private void AttemptPersuasion(int damageDone)
    {
        if (attemptsLeft <= 0)
        {
            Debug.Log($"❌ {recruit.baseData.characterName} lost interest and left...");
            recruitmentActive = false;
            return;
        }

        attemptsLeft--;
        persuasionChance += damageDone * 0.5f; // scale chance by damage done
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

    private void AddRecruitToTeam()
    {
        var playerTeam = PersistentPlayerData.Instance.GetAllPlayerRuntimes();

        if (playerTeam.Count < 3)
        {
            PersistentPlayerData.Instance.UpdateFromRuntime(recruit);
            Debug.Log($"🎉 {recruit.baseData.characterName} joined your team!");
        }
        else
        {
            Debug.Log("⚠️ Team full! Press 1, 2, or 3 to choose a member to replace.");

            StartCoroutine(WaitForReplacementChoice(playerTeam));
        }
    }

    private IEnumerator WaitForReplacementChoice(List<CharacterRuntime> team)
    {
        bool replaced = false;

        while (!replaced)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
                replaced = ReplaceMember(team[0]);
            else if (Input.GetKeyDown(KeyCode.Alpha2))
                replaced = ReplaceMember(team[1]);
            else if (Input.GetKeyDown(KeyCode.Alpha3))
                replaced = ReplaceMember(team[2]);

            yield return null;
        }
    }

    private bool ReplaceMember(CharacterRuntime oldMember)
    {
        Debug.Log($"👋 {oldMember.baseData.characterName} leaves the team. {recruit.baseData.characterName} joins!");
        PersistentPlayerData.Instance.UpdateFromRuntime(recruit);

        // Remove old member
        PersistentPlayerData.Instance.RemoveCharacter(oldMember.baseData.characterName);
        return true;
    }

    public void ResetRecruitment()
{
    recruit = null;
    attemptsLeft = 0;
    persuasionChance = 0f;
    recruitmentActive = false;
}
}
