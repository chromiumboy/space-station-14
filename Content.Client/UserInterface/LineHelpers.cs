using System.Numerics;

namespace Content.Client.UserInterface;

public static class LineHelpers
{
    /// <summary>
    /// This function calculates the vertices of a collection of lines.
    /// Specify the starting and ending vector positions of each line, along with their thickness.
    /// </summary>
    public static List<(Vector2, Vector2, Vector2, Vector2)> CalculateLineVertices(ICollection<(Vector2, Vector2, float)> lines)
    {
        var vertices = new List<(Vector2, Vector2, Vector2, Vector2)>();

        if (lines.Count == 0)
            return vertices;

        foreach (var line in lines)
            vertices.Add(CalculateLineVertices(line));

        return vertices;
    }

    /// <summary>
    /// This function calculates the vertices of a line.
    /// Specify its start and end vector positions along with its thickness.
    /// </summary>
    public static (Vector2, Vector2, Vector2, Vector2) CalculateLineVertices((Vector2, Vector2, float) line)
    {
        var start = line.Item1;
        var end = line.Item2;
        var thickness = line.Item3 / 2f;

        var angle = -MathF.Atan2(end.Y - start.Y, end.X - start.X);
        var offsetAngle = angle + MathF.PI / 2;

        var offsetA = new Vector2(-1 * (thickness * MathF.Cos(angle) - thickness * MathF.Sin(angle)),
            thickness * MathF.Sin(angle) + thickness * MathF.Cos(angle));

        var offsetB = new Vector2(thickness * MathF.Cos(offsetAngle) - thickness * MathF.Sin(offsetAngle),
            -1 * (thickness * MathF.Sin(offsetAngle) + thickness * MathF.Cos(offsetAngle)));

        var offsetC = new Vector2(-1 * (thickness * MathF.Cos(offsetAngle) - thickness * MathF.Sin(offsetAngle)),
            thickness * MathF.Sin(offsetAngle) + thickness * MathF.Cos(offsetAngle));

        var offsetD = new Vector2(thickness * MathF.Cos(angle) - thickness * MathF.Sin(angle),
            -1 * (thickness * MathF.Sin(angle) + thickness * MathF.Cos(angle)));

        return (start + offsetA, start + offsetB, end + offsetC, end + offsetD);
    }
}
