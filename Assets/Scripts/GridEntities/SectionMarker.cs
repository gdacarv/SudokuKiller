using UnityEngine;

[ExecuteAlways]
public class SectionMarker : GridEntityMarker
{
    public int sectionId = 0;

    // Golden-ratio hue spread across 30 possible sections, 55% alpha
    private static readonly float GoldenAngle = 0.618033988749895f;
    private const float Alpha = 0.55f;

    protected override void Update()
    {
        base.Update();
        ApplyColor();
    }

    private void ApplyColor()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr == null) return;
        float hue = Mathf.Repeat(sectionId * GoldenAngle, 1f);
        Color c = Color.HSVToRGB(hue, 0.65f, 0.90f);
        c.a = Alpha;
        sr.color = c;
    }

    public override void ApplyRule(GridManager manager)
    {
        manager.RegisterSection(row, col, sectionId);
    }
}
