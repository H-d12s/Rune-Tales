using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewRegion", menuName = "RPG/Region")]
public class RegionData : ScriptableObject
{
    [Header("Region Info")]
    public string regionName;
    public Sprite backgroundImage;

    [Header("Encounter Settings")]
    [Tooltip("Minimum level range for enemies in this region.")]
    public int minLevel = 1;
    [Tooltip("Maximum level range for enemies in this region.")]
    public int maxLevel = 10;

    [Header("Enemies in this Region")]
    public List<CharacterData> possibleEnemies = new List<CharacterData>();

    [Header("Boss of the Region")]
    public CharacterData bossEnemy;

    [Header("Optional Recruitable Ally (after boss)")]
    public CharacterData recruitableCharacter;
}
