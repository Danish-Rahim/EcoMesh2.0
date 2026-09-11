using UnityEngine;

public class RefineryInteractiveElement : MonoBehaviour
{
    [Header("Popup Information")]
    public string elementTitle;

    [TextArea(5, 10)]
    public string elementExplanation;

    // Optional: Where the cone/focus should point to. 
    // If left empty, the script will just use the center of the object.
    public Transform customTargetPoint;
}