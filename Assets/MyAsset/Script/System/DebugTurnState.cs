using System.Text;
using System.Collections;
using UnityEngine;

/// <summary>
/// Debug helper: dumps TurnBaseSystem runtime lists to Console for diagnosis.
/// Attach to any GameObject and Play — it will log once after a short delay.
/// </summary>
public class DebugTurnState : MonoBehaviour
{
    public float delaySeconds = 0.2f;
    public bool runOnStart = true;

    IEnumerator Start()
    {
        if (!runOnStart) yield break;
        yield return new WaitForSeconds(delaySeconds);
        Dump();
    }

    [ContextMenu("Dump TurnBaseSystem State")]
    public void Dump()
    {
        var tbs = TurnBaseSystem.Instance;
        if (tbs == null)
        {
            Debug.LogWarning("[DebugTurnState] TurnBaseSystem.Instance is null.");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("[DebugTurnState] TurnBaseSystem state dump:");
        sb.AppendLine($"  state = {tbs.state}");
        sb.AppendLine($"  roundNumber = {tbs.roundNumber}");
        sb.AppendLine($"  selectedMonster = {(tbs.selectedMonster != null ? tbs.selectedMonster.name : "null")}");

        if (tbs.characterObjects == null) sb.AppendLine("  characterObjects = null");
        else
        {
            sb.AppendLine($"  characterObjects.Count = {tbs.characterObjects.Count}");
            for (int i = 0; i < tbs.characterObjects.Count; i++)
            {
                var go = tbs.characterObjects[i];
                sb.AppendLine($"    [{i}] = {(go != null ? go.name : "null")}");
            }
        }

        if (tbs.battlers == null) sb.AppendLine("  battlers = null");
        else sb.AppendLine($"  battlers.Count = {tbs.battlers.Count}");
        if (tbs.battlerObjects == null) sb.AppendLine("  battlerObjects = null");
        else sb.AppendLine($"  battlerObjects.Count = {tbs.battlerObjects.Count}");

        int max = Mathf.Max(tbs.battlers?.Count ?? 0, tbs.battlerObjects?.Count ?? 0);
        for (int i = 0; i < max; i++)
        {
            string goName = (tbs.battlerObjects != null && i < tbs.battlerObjects.Count && tbs.battlerObjects[i] != null) ? tbs.battlerObjects[i].name : "null";
            string binfo = "noBattler";
            if (tbs.battlers != null && i < tbs.battlers.Count && tbs.battlers[i] != null)
            {
                var b = tbs.battlers[i];
                binfo = $"name={b.name} hp={b.hp} isMonster={b.isMonster} atk={b.atk} def={b.def} spd={b.speed}";
            }
            sb.AppendLine($"    idx={i} GO={goName} | {binfo}");
        }

        Debug.Log(sb.ToString());
    }
}