using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class BattleUIManager : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject mainActionPanel;
    public GameObject attackSelectionPanel;

    [Header("Buttons")]
    public Button attackButton;
    public Button retreatButton;
    public List<Button> attackButtons;

    [Header("References")]
    public CharacterBattleController playerController;

    private CharacterRuntime playerRuntime;
    private CharacterBattleController currentTarget;
    private BattleManager battleManager;
    private bool isSelectingTarget = false;

    void Start()
    {
        StartCoroutine(InitializeUI());
    }

    private System.Collections.IEnumerator InitializeUI()
    {
        yield return null;

        battleManager = FindObjectOfType<BattleManager>();
        if (playerController == null)
        {
            Debug.LogError("❌ No Player Character linked!");
            yield break;
        }

        playerRuntime = playerController.GetRuntimeCharacter();
        attackSelectionPanel.SetActive(false);

        attackButton.onClick.AddListener(OnAttackPressed);
        retreatButton.onClick.AddListener(OnRetreatPressed);
    }

    void OnAttackPressed()
    {
        mainActionPanel.SetActive(false);
        attackSelectionPanel.SetActive(true);
        isSelectingTarget = true;
        UpdateAttackButtons();
    }

    void OnRetreatPressed()
    {
        Debug.Log("🏃 Retreat pressed (todo)");
    }

    void UpdateAttackButtons()
    {
        var attacks = playerRuntime.equippedAttacks;
        for (int i = 0; i < attackButtons.Count; i++)
        {
            var button = attackButtons[i];
            if (i < attacks.Count)
            {
                var attack = attacks[i];
                button.gameObject.SetActive(true);
                button.GetComponentInChildren<TextMeshProUGUI>().text = attack.attackName;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => OnAttackChosen(attack));
            }
            else
            {
                button.gameObject.SetActive(false);
            }
        }
    }

  void OnAttackChosen(AttackData attack)
{
    if (currentTarget == null)
    {
        Debug.LogWarning("⚠️ No target selected!");
        return;
    }

    Debug.Log($"🎯 {playerRuntime.baseData.characterName} used {attack.attackName} on {currentTarget.characterData.characterName}");

    if (attack.currentUsage > 0)
        attack.currentUsage--;

    // ✅ Deal damage via BattleManager
    battleManager.PerformAttack(playerController, currentTarget, attack);

    // ✅ Reset enemy highlight after attacking
    var selector = currentTarget.GetComponent<EnemySelector>();
    if (selector != null)
        selector.Highlight(false); // turn off highlight

    // ✅ Reset panels
    attackSelectionPanel.SetActive(false);
    mainActionPanel.SetActive(true);

    // ✅ Clear targeting state
    isSelectingTarget = false;
    currentTarget = null;
}
    public void SetPlayerController(CharacterBattleController controller)
    {
        playerController = controller;
        playerRuntime = controller.GetRuntimeCharacter();
        UpdateAttackButtons();
    }

    public void SetTarget(CharacterBattleController target)
    {
        if (!isSelectingTarget || target.isPlayer)
            return;

        currentTarget = target;
        Debug.Log($"🎯 Target selected: {target.characterData.characterName}");
    }

    public bool IsSelectingTarget() => isSelectingTarget;
}
