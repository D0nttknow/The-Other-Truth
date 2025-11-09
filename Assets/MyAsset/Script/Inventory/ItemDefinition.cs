using UnityEngine;

[CreateAssetMenu(fileName = "NewItem", menuName = "Inventory/ItemDefinition")]
public class ItemDefinition : ScriptableObject
{
    public string id;            // unique id (ex: "potion_small")
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;
    public bool stackable = true;
    public int maxStack = 99;
    public int baseValue = 0;    // gold / price / or exp value if used
}