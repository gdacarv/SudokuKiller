using UnityEngine;

[ExecuteAlways]
public class SectionMarker : GridEntityMarker
{
    public int sectionId = 0;

    // Golden-ratio hue spread across 30 possible sections
    private const float GoldenAngle = 0.618033988749895f;

    public static Color GetSectionColor(int id, float alpha = 0.25f)
    {
        float hue = Mathf.Repeat(id * GoldenAngle, 1f);
        Color c = Color.HSVToRGB(hue, 0.65f, 0.90f);
        c.a = alpha;
        return c;
    }

    protected override void Update()
    {
        base.Update();
        ApplyColor();
    }

    private void ApplyColor()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr == null) return;
        sr.color = GetSectionColor(sectionId);
    }

    public override void ApplyRule(GridManager manager)
    {
        manager.RegisterSection(row, col, sectionId);
    }
}
