using UnityEngine;

/// <summary>
/// Off-grid slot a suspect can be dragged into to switch the board's click/drag tool
/// from placing user blockers to placing translucent draft copies of that suspect.
/// Dropping a second suspect in swaps it for the first, which returns to its spawn.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class DraftHolder : MonoBehaviour
{
    public GridManager gridManager;
    public GameObject emptyIndicator;

    private Collider2D _collider;

    public Draggable Held { get; private set; }

    public bool Accepts => gridManager != null && gridManager.enableUserBlockers;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();

        if (emptyIndicator == null)
        {
            var child = transform.Find("BlockedGridCell");
            if (child != null) emptyIndicator = child.gameObject;
        }
    }
#endif

    private void Awake()
    {
        _collider = GetComponent<Collider2D>();
    }

    private void Start()
    {
        RefreshIndicator();
    }

    public bool Contains(Vector3 worldPos) => _collider.OverlapPoint(worldPos);

    public void Hold(Draggable suspect)
    {
        if (Held != null && Held != suspect)
            Held.ReturnToSpawn();

        Held = suspect;
        suspect.transform.position = transform.position;
        RefreshIndicator();
    }

    public void Release(Draggable suspect)
    {
        if (Held != suspect) return;
        Held = null;
        RefreshIndicator();
    }

    private void RefreshIndicator()
    {
        if (emptyIndicator != null)
            emptyIndicator.SetActive(Held == null);
    }
}
