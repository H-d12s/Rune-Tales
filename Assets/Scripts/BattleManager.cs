using UnityEngine;
using System.Collections.Generic;

public class BattleManager : MonoBehaviour
{
    [Header("Spawn Points")]
    public Transform[] playerSpawnPoints;
    public Transform[] enemySpawnPoints;

    [Header("Prefabs")]
    public GameObject characterPrefab; // The character prefab (used for both player & enemies)

    [Header("Team Data")]
    public List<CharacterData> playerTeam;
    public List<CharacterData> enemyTeam;

    private List<CharacterBattleController> playerControllers = new List<CharacterBattleController>();
    private List<CharacterBattleController> enemyControllers = new List<CharacterBattleController>();

    private BattleUIManager uiManager;

    // Hardcoded offset to lower characters on screen
    [Header("Spawn Offset")]
    public float verticalOffset = -1.5f;

    void Start()
    {
        // Find the UI manager in the scene
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

        // ✅ After spawning, connect the *first* player to the UI
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
            var spawnPoint = spawnPoints[i];
            if (spawnPoint == null)
            {
                Debug.LogWarning($"⚠️ Missing spawn point {i} for {(isPlayerTeam ? "player" : "enemy")}!");
                continue;
            }

            // Offset position downward
            Vector3 spawnPos = spawnPoint.position + new Vector3(0f, verticalOffset, 0f);

            // Instantiate prefab
            GameObject characterObj = Instantiate(characterPrefab, spawnPos, Quaternion.identity);
            characterObj.name = $"{(isPlayerTeam ? "Player" : "Enemy")}_{teamData[i].characterName}";

            // Maintain prefab's scale (10,10,1)
            characterObj.transform.localScale = new Vector3(10f, 10f, 1f);

            // Ensure it has controller
            var controller = characterObj.GetComponent<CharacterBattleController>();
            if (controller == null)
            {
                Debug.LogError("❌ CharacterPrefab missing CharacterBattleController component!");
                continue;
            }

            // Assign data and initialize
            controller.characterData = teamData[i];
            controller.InitializeCharacter();

            // Flip enemies horizontally (preserve scale)
            if (!isPlayerTeam)
            {
                var scale = characterObj.transform.localScale;
                scale.x *= -1;
                characterObj.transform.localScale = scale;
            }

            controllerList.Add(controller);
        }
    }
}
