using System.Collections;
using UnityEngine;

public class MessageTester : MonoBehaviour
{
    public BattleMessageUI messageUI;

    private void Start()
    {
        StartCoroutine(TestMessages());
    }

    private IEnumerator TestMessages()
    {
        yield return StartCoroutine(messageUI.ShowMessage("🔥 Battle Start!"));
        yield return StartCoroutine(messageUI.ShowMessage("Hero used Slash!"));
        yield return StartCoroutine(messageUI.ShowMessage("🎲 Dice roll successful!"));
        yield return StartCoroutine(messageUI.ShowMessage("💀 Goblin fainted!"));
        yield return StartCoroutine(messageUI.ShowMessage("🏆 Victory!"));
    }
}
