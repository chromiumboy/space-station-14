using Content.Client.Pinpointer.UI;
using Content.Shared.Atmos.Components;
using Content.Shared.Pinpointer;
using Robust.Client.Graphics;
using Robust.Shared.Collections;
using Robust.Shared.Map.Components;
using System.Numerics;

namespace Content.Client.Atmos.Console;

public sealed partial class AtmosMonitoringConsoleNavMapControl : NavMapControl
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    private Dictionary<Color, Color> _sRGBLookUp = new Dictionary<Color, Color>();

    public Dictionary<Vector2i, List<AtmosMonitoringConsoleLine>>? AtmosPipeNetwork;

    private MapGridComponent? _grid;

    public AtmosMonitoringConsoleNavMapControl() : base()
    {
        // Set colors
        //TileColor = Color.DarkSlateGray;  //new Color(70, 70, 10);
        //WallColor = Color.LightSlateGray; //new Color(186, 186, 13);

        WallColor = new Color(180, 145, 0);
        TileColor = Color.DimGray * WallColor;

        _backgroundColor = Color.FromSrgb(TileColor.WithAlpha(_backgroundOpacity));

        PostWallDrawingAction += DrawAllPipeNetworks;
    }

    protected override void UpdateNavMap()
    {
        base.UpdateNavMap();

        if (Owner == null)
            return;

        if (!_entManager.TryGetComponent<AtmosMonitoringConsoleComponent>(Owner, out var console))
            return;

        if (!_entManager.TryGetComponent(MapUid, out _grid))
            return;

        AtmosPipeNetwork = GetDecodedAtmosPipeChunks(console.AllChunks, _grid);
    }

    public void DrawAllPipeNetworks(DrawingHandleScreen handle)
    {
        // Draw network
        if (AtmosPipeNetwork != null && AtmosPipeNetwork.Count > 0)
        {
            DrawPipeNetwork(handle, AtmosPipeNetwork);
        }
    }

    public void DrawPipeNetwork(DrawingHandleScreen handle, Dictionary<Vector2i, List<AtmosMonitoringConsoleLine>> atmosPipeNetwork)
    {
        var offset = GetOffset();
        var area = new Box2(-WorldRange, -WorldRange, WorldRange + 1f, WorldRange + 1f).Translated(offset);

        if (WorldRange / WorldMaxRange > 0.5f)
        {
            var pipeNetworks = new Dictionary<Color, ValueList<Vector2>>();

            foreach ((var chunk, var chunkedLines) in atmosPipeNetwork)
            {
                var offsetChunk = new Vector2(chunk.X, chunk.Y) * SharedNavMapSystem.ChunkSize;

                if (offsetChunk.X < area.Left - SharedNavMapSystem.ChunkSize || offsetChunk.X > area.Right)
                    continue;

                if (offsetChunk.Y < area.Bottom - SharedNavMapSystem.ChunkSize || offsetChunk.Y > area.Top)
                    continue;

                foreach (var chunkedLine in chunkedLines)
                {
                    var start = Scale(chunkedLine.Origin - new Vector2(offset.X, -offset.Y));
                    var end = Scale(chunkedLine.Terminus - new Vector2(offset.X, -offset.Y));

                    if (!pipeNetworks.TryGetValue(chunkedLine.Color, out var subNetwork))
                        subNetwork = new ValueList<Vector2>();

                    subNetwork.Add(start);
                    subNetwork.Add(end);

                    pipeNetworks[chunkedLine.Color] = subNetwork;
                }
            }

            foreach ((var color, var subNetwork) in pipeNetworks)
            {
                if (subNetwork.Count > 0)
                {
                    if (!_sRGBLookUp.TryGetValue(color, out var sRGB))
                    {
                        sRGB = Color.ToSrgb(color);
                        _sRGBLookUp[color] = sRGB;
                    }

                    handle.DrawPrimitives(DrawPrimitiveTopology.LineList, subNetwork.Span, sRGB);
                }
            }
        }

        else
        {
            var pipeVertexUVs = new Dictionary<Color, ValueList<Vector2>>();

            foreach ((var chunk, var chunkedLines) in atmosPipeNetwork)
            {
                var offsetChunk = new Vector2(chunk.X, chunk.Y) * SharedNavMapSystem.ChunkSize;

                if (offsetChunk.X < area.Left - SharedNavMapSystem.ChunkSize || offsetChunk.X > area.Right)
                    continue;

                if (offsetChunk.Y < area.Bottom - SharedNavMapSystem.ChunkSize || offsetChunk.Y > area.Top)
                    continue;

                foreach (var chunkedLine in chunkedLines)
                {
                    var leftTop = Scale(new Vector2
                        (Math.Min(chunkedLine.Origin.X, chunkedLine.Terminus.X) - 0.1f,
                        Math.Min(chunkedLine.Origin.Y, chunkedLine.Terminus.Y) - 0.1f)
                        - new Vector2(offset.X, -offset.Y));

                    var rightTop = Scale(new Vector2
                        (Math.Max(chunkedLine.Origin.X, chunkedLine.Terminus.X) + 0.1f,
                        Math.Min(chunkedLine.Origin.Y, chunkedLine.Terminus.Y) - 0.1f)
                        - new Vector2(offset.X, -offset.Y));

                    var leftBottom = Scale(new Vector2
                        (Math.Min(chunkedLine.Origin.X, chunkedLine.Terminus.X) - 0.1f,
                        Math.Max(chunkedLine.Origin.Y, chunkedLine.Terminus.Y) + 0.1f)
                        - new Vector2(offset.X, -offset.Y));

                    var rightBottom = Scale(new Vector2
                        (Math.Max(chunkedLine.Origin.X, chunkedLine.Terminus.X) + 0.1f,
                        Math.Max(chunkedLine.Origin.Y, chunkedLine.Terminus.Y) + 0.1f)
                        - new Vector2(offset.X, -offset.Y));

                    if (!pipeVertexUVs.TryGetValue(chunkedLine.Color, out var pipeVertexUV))
                        pipeVertexUV = new ValueList<Vector2>();

                    pipeVertexUV.Add(leftBottom);
                    pipeVertexUV.Add(leftTop);
                    pipeVertexUV.Add(rightBottom);
                    pipeVertexUV.Add(leftTop);
                    pipeVertexUV.Add(rightBottom);
                    pipeVertexUV.Add(rightTop);

                    pipeVertexUVs[chunkedLine.Color] = pipeVertexUV;
                }
            }

            foreach ((var color, var pipeVertexUV) in pipeVertexUVs)
            {
                if (pipeVertexUV.Count > 0)
                {
                    if (!_sRGBLookUp.TryGetValue(color, out var sRGB))
                    {
                        sRGB = Color.ToSrgb(color);
                        _sRGBLookUp[color] = sRGB;
                    }

                    handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, pipeVertexUV.Span, sRGB);
                }
            }
        }
    }

    public Dictionary<Vector2i, List<AtmosMonitoringConsoleLine>>? GetDecodedAtmosPipeChunks(Dictionary<Vector2i, AtmosPipeChunk>? chunks, MapGridComponent? grid)
    {
        if (chunks == null || grid == null)
            return null;

        var decodedOutput = new Dictionary<Vector2i, List<AtmosMonitoringConsoleLine>>();

        foreach ((var chunkOrigin, var chunk) in chunks)
        {
            var list = new List<AtmosMonitoringConsoleLine>();

            foreach ((var hexColor, var atmosPipeData) in chunk.AtmosPipeData)
            {
                for (var chunkIdx = 0; chunkIdx < SharedNavMapSystem.ChunkSize * SharedNavMapSystem.ChunkSize; chunkIdx++)
                {
                    var value = (int) Math.Pow(2, chunkIdx);

                    var northMask = atmosPipeData.NorthFacing & value;
                    var southMask = atmosPipeData.SouthFacing & value;
                    var eastMask = atmosPipeData.EastFacing & value;
                    var westMask = atmosPipeData.WestFacing & value;

                    if ((northMask | southMask | eastMask | westMask) == 0)
                        continue;

                    var relativeTile = SharedNavMapSystem.GetTile(value);
                    var tile = (chunk.Origin * SharedNavMapSystem.ChunkSize + relativeTile) * grid.TileSize;
                    var position = new Vector2(tile.X, -tile.Y);

                    var lineLongitudinalOrigin = (northMask > 0) ?
                        new Vector2(grid.TileSize * 0.5f, -grid.TileSize * 1f) : new Vector2(grid.TileSize * 0.5f, -grid.TileSize * 0.5f);
                    var lineLongitudinalTerminus = (southMask > 0) ?
                        new Vector2(grid.TileSize * 0.5f, -grid.TileSize * 0f) : new Vector2(grid.TileSize * 0.5f, -grid.TileSize * 0.5f);
                    var lineLateralOrigin = (eastMask > 0) ?
                        new Vector2(grid.TileSize * 1f, -grid.TileSize * 0.5f) : new Vector2(grid.TileSize * 0.5f, -grid.TileSize * 0.5f);
                    var lineLateralTerminus = (westMask > 0) ?
                        new Vector2(grid.TileSize * 0f, -grid.TileSize * 0.5f) : new Vector2(grid.TileSize * 0.5f, -grid.TileSize * 0.5f);

                    // Add points
                    var color = Color.FromHex(hexColor) * Color.DarkGray;

                    var lineLongitudinal = new AtmosMonitoringConsoleLine(position + lineLongitudinalOrigin, position + lineLongitudinalTerminus, color);
                    list.Add(lineLongitudinal);

                    var lineLateral = new AtmosMonitoringConsoleLine(position + lineLateralOrigin, position + lineLateralTerminus, color);
                    list.Add(lineLateral);
                }
            }

            if (list.Count > 0)
                decodedOutput.Add(chunkOrigin, list);
        }

        return decodedOutput;
    }
}

public struct AtmosMonitoringConsoleLine
{
    public readonly Vector2 Origin;
    public readonly Vector2 Terminus;
    public readonly Color Color;

    public AtmosMonitoringConsoleLine(Vector2 origin, Vector2 terminus, Color color)
    {
        Origin = origin;
        Terminus = terminus;
        Color = color;
    }
}
