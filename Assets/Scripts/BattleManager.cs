using UnityEngine;
using System.Collections.Generic;

public class BattleManager : MonoBehaviour
{
    [Header("Spawn Points")]
    public Transform[] playerSpawnPoints;
    public Transform[] enemySpawnPoints;

    [Header("Prefabs")]
    public GameObject characterPrefab; // Used for both player & enemies

    [Header("Team Data")]
    public List<CharacterData> playerTeam;
    public List<CharacterData> enemyTeam;

    private List<CharacterBattleController> playerControllers = new List<CharacterBattleController>();
    private List<CharacterBattleController> enemyControllers = new List<CharacterBattleController>();

    private BattleUIManager uiManager;

    [Header("Spawn Offset")]
    public float verticalOffset = -1.5f;

    void Start()
    {
        uiManager = FindObjectOfType<BattleUIManager>();
        if (uiManager == null)
        {
            Debug.LogError("❌ No BattleUIManager found in the scene!");
            return;
        }

        // Spawn both teams
        SpawnTeam(playerTeam, playerSpawnPoints, playerControllers, true);
        SpawnTeam(enemyTeam, enemySpawnPoints, enemyControllers, false);

        Debug.Log($"✅ Battle started: {playerControllers.Count} players vs {enemyControllers.Count} enemies");

        // Connect first player to UI
        if (playerControllers.Count > 0)
        {
            uiManager.SetPlayerController(playerControllers[0]);
            Debug.Log("✅ Linked player controller to Battle UI Manager.");
        }
        else
        {
            Debug.LogWarning("⚠️ No player characters spawned — UI will remain inactive.");
        }
    }

   private void SpawnTeam(List<CharacterData> teamData, Transform[] spawnPoints,
                       List<CharacterBattleController> controllerList, bool isPlayerTeam)
{
    for (int i = 0; i < teamData.Count && i < spawnPoints.Length; i++)
    {
        Transform spawnPoint = spawnPoints[i];
        if (spawnPoint == null)
        {
            Debug.LogWarning($"⚠️ Missing spawn point {i} for {(isPlayerTeam ? "player" : "enemy")}!");
            continue;
        }

        // ✅ Apply vertical offset (lower them visually)
        Vector3 spawnPos = spawnPoint.position + new Vector3(0, verticalOffset, 0);

        GameObject characterObj = Instantiate(characterPrefab, spawnPos, Quaternion.identity);
        characterObj.name = $"{(isPlayerTeam ? "Player" : "Enemy")}_{teamData[i].characterName}";

        var controller = characterObj.GetComponent<CharacterBattleController>();
        if (controller == null)
        {
            Debug.LogError("❌ CharacterPrefab missing CharacterBattleController component!");
            continue;
        }

        controller.characterData = teamData[i];
        controller.InitializeCharacter();

        // Handle EnemySelector
        var selector = characterObj.GetComponent<EnemySelector>();
        if (selector == null)
            selector = characterObj.AddComponent<EnemySelector>();

        var collider = characterObj.GetComponent<Collider2D>();
        if (collider == null)
            collider = characterObj.AddComponent<BoxCollider2D>();

        collider.isTrigger = true;

        if (isPlayerTeam)
        {
            selector.enabled = false;
            collider.enabled = false;
        }
        else
        {
            selector.enabled = true;
            collider.enabled = true;

            // Flip enemies horizontally
            var scale = characterObj.transform.localScale;
            scale.x = Mathf.Abs(scale.x) * -1; // ensure consistent flip
            characterObj.transform.localScale = scale;
        }

        controllerList.Add(controller);
    }
}


}
