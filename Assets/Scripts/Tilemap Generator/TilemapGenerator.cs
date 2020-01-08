using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// (Topdown) Tilemap Generator
///
/// Generates one or more traditional or hexagonal tilemaps based on provided Rule Tiles and a source (empty) Tilemap that will be used as a reference.
/// Each Rule Tile will represent a "layer", calculated using Perlin Noise, and will be placed on its own Tilemap.
/// The "Sorting Order" of any generated Tilemap (Renderer) will be incremented depending on the calculated layer.
/// You are also required to provide a "Weight" for each Rule Tile/layer. The weight defines "how big" a layer should be compared to the others.
/// The generated tilemaps will be created as children of the empty template ones, to avoid messing up with other tilemaps that you may want to use outside the scope of this script.
///
/// Parameters available from the Inspector:
/// - Rule Tiles            add a Rule Tile for each layer that you want to create
/// - Weights               assign a weight to each layer to define how big it should be compared to the other ones.
/// - Width                 the width of the Map
/// - Height                the height of the Map
/// - Scale                 affects the Perlin Noise calculation; you may want to adjust this depending on the Width and Height of your Map
/// - Randomize             use a random Seed; ignored if calling Generate(true) from script, but not with Generate() or Generate(false)
/// - Seed                  base for the Perlin Noise calculation; the same Seed (with the same Scale) will always generate the same Map
/// - Center Map            Cell(0, 0) will be in the center of the generated Map
/// - Fade Out Tiles        check this option if your rule tiles only provide rules to fade out to the next layer
/// - Island Water Border   if greater than zero, fades the edges of the Map to the first layer and then adds a border of the specified size around the Map
///
/// Usage
/// - Download and install the required Unity 2d-extras package from "https://github.com/Unity-Technologies/2d-extras"
///    As specified in the README.md file of that repository, you can just add
///    "com.unity.2d.tilemap.extras": "https://github.com/Unity-Technologies/2d-extras.git#master"
///    to your Packages/manifest.json file in your Unity Project under the dependencies section
/// - Create an empty Tilemap, either for traditional or hexagonal tiles, that will be used as a template for the generated ones
/// - Attach this script to the Grid GameObject containing the template Tilemap
/// - In the Inspector, assign a Rule Tile for any layer you want in the "Rule Tiles" list
/// - Define a weight for each layer in the "Weights" list
/// - Fill up the additional parameters as explained above
/// - Press "Generate Tilemaps" in the Editor to create the tilemaps, or call the Generate() method (provided by the API) from another script
///
/// Additional API methods and references
/// - grid                  The grid component to which this script is attached
/// - Map                   TileData matrix representing the (calculated) tiles
/// - TilemapsGenerated     True if any generated Tilemap already exists
/// - CellToMap()           Converts a position from Cell to Map
/// - MapToCell()           Converts a position from Map to Cell
/// - GetTileDataMap()      Returns a TileData object from the specified position in the Map
/// - GetTileDataCell()     Returns a TileData object from the specified position in the grid
/// - GetTileDataWorld()    Returns a TileData object from the specified World position
/// - Generate()            Generates (or just calculates, if passing "true" as parameter) new Tilemaps
///
/// If you generated tilemaps from the Inspector and you want to use the Tilemap Generator API from your scripts during run-time, remember to always call "Generate(true)" first,
/// before any other provided function, using the same parameters in the Inspector that generated those tilemaps.
/// </summary>
public class TilemapGenerator : MonoBehaviour
{
    public enum IslandShape
    {
        None,
        Rectangle,
        Ellipse
    }

    public Tilemap sourceTilemap;
    public RuleTile[] ruleTiles;
    public float[] weights;
    public int width;
    public int height;
    [Range(0f, 1f)]
    public float scale = .5f;
    public bool randomize;
    public float seed;
    public bool centerMap;
    public bool fadeOutTiles;
    public int islandWaterBorder;
    public IslandShape islandShape;
    
    TileData[,] _tiles;
    Tilemap[] _tilemaps;
    int _zLayers;
    float _totalWeight;

    [HideInInspector]
    public Grid grid;

    /// <summary>
    /// TileData matrix representing the (calculated) tiles
    /// </summary>
    /// <returns>
    /// A two-dimensional aray of TileData objects
    /// </returns>
    public TileData[,] Map
    {
        get
        {
            return _tiles;
        }
    }

    /// <summary>
    /// Checks if any generated Tilemap already exists
    /// </summary>
    /// <returns>
    /// "true" if tilemaps were found, otherwise "false"
    /// </returns>
    public bool TilemapsGenerated
    {
        get
        {
            return sourceTilemap.transform.childCount > 0;
        }
    }

    /// <summary>
    /// Distance from cell World Position to the actual center
    /// </summary>
    /// <returns>
    /// A Vector3 containing the offset to add to the cell position for getting the center of a cell
    /// </returns>
    public Vector3 CellOffset
    {
        get
        {
            return new Vector3(grid.cellSize.x / 2f, grid.cellSize.y / 2f, 0f);
        }
    }

    /// <summary>
    /// Converts a Vector3Int position from Cell to Map
    /// </summary>
    /// <param name="pos">Cell position in the grid</param>
    /// <returns>
    /// A Vector3Int representing the requested position
    /// </returns>
    public Vector3Int CellToMap(Vector3Int pos)
    {
        return new Vector3Int(centerMap ? pos.x + width / 2 : pos.x, centerMap ? pos.y + height / 2 : pos.y, 0);
    }

    /// <summary>
    /// Converts a (x, y) position from Cell to Map
    /// </summary>
    /// <param name="x">Cell X positon in the grid</param>
    /// <param name="y">Cell Y positon in the grid</param>
    /// <returns>
    /// A Vector3Int representing the requested position
    /// </returns>
    public Vector3Int CellToMap(int x, int y)
    {
        return new Vector3Int(centerMap ? x + width / 2 : x, centerMap ? y + height / 2 : y, 0);
    }

    /// <summary>
    /// Converts a Vector3Int position from Map to Cell
    /// </summary>
    /// <param name="pos">Map positon</param>
    /// <returns>
    /// A Vector3Int representing the requested position
    /// </returns>
    public Vector3Int MapToCell(Vector3Int pos)
    {
        return new Vector3Int(centerMap ? pos.x - width / 2 : pos.x, centerMap ? pos.y - height / 2 : pos.y, 0);
    }

    /// <summary>
    /// Converts a (x, y) position from Map to Cell
    /// </summary>
    /// <param name="x">Map X positon</param>
    /// <param name="y">Map Y positon</param>
    /// <returns>
    /// A Vector3Int representing the requested position
    /// </returns>
    public Vector3Int MapToCell(int x, int y)
    {
        return new Vector3Int(centerMap ? x - width / 2 : x, centerMap ? y - height / 2 : y, 0);
    }

    /// <summary>
    /// Returns a TileData object from the specified position in the Map
    /// </summary>
    /// <param name="pos">Map positon</param>
    /// <returns>
    /// The requested TileData, or "null" if not found
    /// </returns>
    public TileData GetTileDataMap(Vector3Int pos)
    {
        if (pos.x >=0 && pos.x < width && pos.y >= 0 && pos.y < height)
        {
            return _tiles[pos.x, pos.y];
        }
        return null;
    }

    /// <summary>
    /// Returns a TileData object from the specified Cell position on the grid
    /// </summary>
    /// <param name="pos">Cells positon</param>
    /// <returns>
    /// The requested TileData, or "null" if not found
    /// </returns>
    public TileData GetTileDataCell(Vector3Int pos)
    {
        return GetTileDataMap(CellToMap(pos));
    }

    /// <summary>
    /// Returns a TileData object from the specified World coordinates
    /// </summary>
    /// <param name="pos">World-Space positon</param>
    /// <returns>
    /// The requested TileData, or "null" if not found
    /// </returns>
    public TileData GetTileDataWorld(Vector3 pos)
    {
        return GetTileDataCell(grid.WorldToCell(pos));
    }

    /// <summary>
    /// Generates (or just calculates, if "existingTiles" is true) new Tilemaps.
    /// </summary>
    /// <param name="existingTiles">Set this to "true" if Tilemaps were already generated. Default: false</param>
    public void Generate(bool existingTiles = false)
    {
        if (ruleTiles.Length < 1)
        {
            Debug.LogError("[Tilemap Generator] Please drag Rule Tiles in the Inspector");
            return;
        }
        else if (ruleTiles.Length > weights.Length)
        {
            Debug.LogError("[Tilemap Generator] Please provide a weight for each Rule Tile");
            return;
        }

        if (islandShape == IslandShape.Ellipse)
        {
            centerMap = true;
        }

        if (grid == null)
        {
            grid = GetComponent<Grid>();
        }

        if (existingTiles)
        {
            randomize = false;
            _tilemaps = new Tilemap[sourceTilemap.transform.childCount];
            for (int i = 0; i < sourceTilemap.transform.childCount; i++)
            {
                _tilemaps[i] = sourceTilemap.transform.GetChild(i).GetComponent<Tilemap>();
            }
        }
        else
        {
            // remove old (generated) tilemaps
            if (sourceTilemap.transform.childCount > 0)
            {
                for (int i = sourceTilemap.transform.childCount -1; i >= 0; i--)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(sourceTilemap.transform.GetChild(i).gameObject);
                    }
                    else
                    {
                        DestroyImmediate(sourceTilemap.transform.GetChild(i).gameObject);
                    }
                }
            }

            _tilemaps = new Tilemap[ruleTiles.Length];
            // Instantiate new tilemaps
            for (int i = 0; i < ruleTiles.Length; i++)
            {
                _tilemaps[i] = Instantiate(sourceTilemap, grid.transform.position, grid.transform.rotation, grid.transform);
                _tilemaps[i].gameObject.name = "Tilemap " + i;
            }

            // Move new tilemaps as children of the source one
            for (int i = 0; i < _tilemaps.Length; i++)
            {
                _tilemaps[i].transform.SetParent(sourceTilemap.transform);
            }
        }

        if (randomize)
        {
            seed = Random.Range(-1024f, 1024f);
        }

        CalculateTotalWeight();

        CalculatePerlinNoise();

        bool makeIsland = (islandShape != IslandShape.None && islandWaterBorder > 0 && _zLayers > 0 && width > _zLayers * 2 && height > _zLayers * 2);
        if (makeIsland)
        {
            MakeIsland();
            if (islandShape == IslandShape.Ellipse)
            {
                MakeIslandEllipse();
            }
        }

        if (!existingTiles)
        {
            // Set tiles
            for (int i = 0; i < _tilemaps.Length; i++)
            {
                _tilemaps[i].GetComponent<TilemapRenderer>().sortingOrder = _tilemaps.Length - i;
                _tilemaps[i].ClearAllTiles();
            }
            for (int y = 0; y < height; y++)
            {
                // place main tiles for each layer
                for (int x = 0; x < width; x++)
                {
                    Vector3Int pos = MapToCell(x, y);
                    int index;
                    if (_zLayers == 0)
                    {
                        index = 0;
                    }
                    else
                    {
                        index = _tiles[x, y].zLayer < _zLayers ? _tiles[x, y].zLayer : _zLayers - 1;
                    }
                    _tilemaps[index].SetTile(pos, ruleTiles[index]);
                }
            }

            if (makeIsland)
            {
                AddWaterBorder();
            }

            if (fadeOutTiles && _zLayers > 0)
            {
                OverlapEdges();
            }
        }
    }


    /* Class methods
     */

    void CalculateTotalWeight()
    {
        _totalWeight = 0f;
        if (weights.Length <= ruleTiles.Length)
        {
            foreach (float weight in weights)
            {
                _totalWeight += weight;
            }
            if (weights.Length < ruleTiles.Length)
            {
                _totalWeight += ruleTiles.Length - weights.Length; // assume 1f is missing weight
            }
        }
        else // ignore extra weights
        {
            for (int i = 0; i < ruleTiles.Length; i++)
            {
                _totalWeight += weights[i];
            }
        }
    }

    void CalculatePerlinNoise()
    {
        _tiles = new TileData[width, height];

        // Calculate "raw" perlin noise for all the tiles
        float zMin = 1f;
        float zMax = 0f;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                _tiles[x, y] = new TileData(x, y);

                //Vector3Int pos = new Vector3Int(centerMap ? x - width / 2 : x, centerMap ? y - height / 2 : y, 0);
                //Vector3 worldPos = grid.CellToWorld(pos);
                Vector3 worldPos = grid.CellToWorld(MapToCell(x, y));
                _tiles[x, y].worldX = worldPos.x;
                _tiles[x, y].worldY = worldPos.y;

                float z = Mathf.PerlinNoise((x + seed) / width / scale, (y + seed) / height / scale);
                _tiles[x, y].z = z;
                if (z < zMin)
                {
                    zMin = z;
                }
                else if (z > zMax)
                {
                    zMax = z;
                }
            }
        }

        // Normalize perlin noise
        _zLayers = _tilemaps.Length > 1 ? _tilemaps.Length : 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float z = (_tiles[x, y].z - zMin) / (zMax - zMin);
                _tiles[x, y].z = z;
                if (_zLayers > 0)
                {
                    float amount = 0f;
                    int zLayer = 0;
                    float weight = zLayer < weights.Length ? weights[zLayer] : 1f;
                    while (amount + weight < z * _totalWeight)
                    {
                        amount += weight;
                        zLayer++;
                        weight = zLayer < weights.Length ? weights[zLayer] : 1f;
                    }
                    _tiles[x, y].zLayer = zLayer < ruleTiles.Length ? zLayer : ruleTiles.Length - 1;
                }
            }
        }
    }

    int EllipseX(int y)
    {
        float a = (float)width / 2f;
        float b = (float)height / 2f;
        float fY = (float)y;

        int x = Mathf.RoundToInt(Mathf.Sqrt((1f - (fY * fY) / (b * b)) * (a * a)));
        if (width % 2 == 0 && x == width / 2)
        {
            x--;
        }

        return x;
    }

    void ClampCellLayer(int x, int y, int zLayer, float zAmount)
    {
        if (x > -width / 2 && x < width / 2 && y > -height / 2 && y < height / 2)
        {
            Vector3Int mapPosition = CellToMap(x, y);
            if (_tiles[mapPosition.x, mapPosition.y].zLayer > zLayer)
            {
                _tiles[mapPosition.x, mapPosition.y].z = zAmount;
                _tiles[mapPosition.x, mapPosition.y].zLayer = zLayer;
            }
        }
    }

    void FadeCellsFrom(int x, int y, bool up, bool right)
    {
        int xCurr = x;
        int yCurr = y;
        int zLayer = 0;
        float zAmount = 0f;
        while (zLayer < _zLayers)
        {
            ClampCellLayer(xCurr, y, zLayer, zAmount);
            ClampCellLayer(x, yCurr, zLayer, zAmount);
            ClampCellLayer(xCurr, yCurr, zLayer, zAmount);

            if (up == true && right == true)
            {
                xCurr--;
                yCurr--;
            }
            else if (up == true && right == false)
            {
                xCurr++;
                yCurr--;
            }
            else if (up == false && right == true)
            {
                xCurr--;
                yCurr++;
            }
            else
            {
                xCurr++;
                yCurr++;
            }

            zLayer++;
            zAmount += zLayer < weights.Length ? weights[zLayer] : 1f;
        }
    }

    void MakeIslandEllipse()
    {
        int xPrev = width / 2; // right edge

        for (int y = 0; y < height / 2; y++)
        {
            int xPositive = EllipseX(y);
            int xNegative = -xPositive;

            // add water over the edge of the ellipse for the current tile
            for (int i = y; i < height / 2; i++)
            {
                Vector3Int mapPositionTopRight = CellToMap(xPositive, i);
                _tiles[mapPositionTopRight.x, mapPositionTopRight.y].z = 0f;
                _tiles[mapPositionTopRight.x, mapPositionTopRight.y].zLayer = 0;

                Vector3Int mapPositionTopLeft = CellToMap(xNegative, i);
                _tiles[mapPositionTopLeft.x, mapPositionTopLeft.y].z = 0f;
                _tiles[mapPositionTopLeft.x, mapPositionTopLeft.y].zLayer = 0;

                Vector3Int mapPositionBottomRight = CellToMap(xPositive, -i);
                _tiles[mapPositionBottomRight.x, mapPositionBottomRight.y].z = 0f;
                _tiles[mapPositionBottomRight.x, mapPositionBottomRight.y].zLayer = 0;

                Vector3Int mapPositionBottomLeft = CellToMap(xNegative, -i);
                _tiles[mapPositionBottomLeft.x, mapPositionBottomLeft.y].z = 0f;
                _tiles[mapPositionBottomLeft.x, mapPositionBottomLeft.y].zLayer = 0;
            }
            FadeCellsFrom(xPositive, y, true, true);
            FadeCellsFrom(xNegative, y, true, false);
            FadeCellsFrom(xPositive, -y, false, true);
            FadeCellsFrom(xNegative, -y, false, false);

            // check for missing tiles (right side)
            if (xPositive < xPrev - 1)
            {
                // make up for the missing tiles on the right
                for (int i = xPrev - 1; i > xPositive; i--)
                {
                    // draw water over the edge of the ellipse for the missing tiles
                    for (int j = y; j < height / 2; j++)
                    {
                        Vector3Int mapPositionTopRight = CellToMap(i, j);
                        _tiles[mapPositionTopRight.x, mapPositionTopRight.y].z = 0f;
                        _tiles[mapPositionTopRight.x, mapPositionTopRight.y].zLayer = 0;

                        Vector3Int mapPositionTopLeft = CellToMap(-i, j);
                        _tiles[mapPositionTopLeft.x, mapPositionTopLeft.y].z = 0f;
                        _tiles[mapPositionTopLeft.x, mapPositionTopLeft.y].zLayer = 0;

                        Vector3Int mapPositionBottomRight = CellToMap(i, -j);
                        _tiles[mapPositionBottomRight.x, mapPositionBottomRight.y].z = 0f;
                        _tiles[mapPositionBottomRight.x, mapPositionBottomRight.y].zLayer = 0;

                        Vector3Int mapPositionBottomLeft = CellToMap(-i, -j);
                        _tiles[mapPositionBottomLeft.x, mapPositionBottomLeft.y].z = 0f;
                        _tiles[mapPositionBottomLeft.x, mapPositionBottomLeft.y].zLayer = 0;
                    }
                    FadeCellsFrom(i, y, true, true);
                    FadeCellsFrom(-i, y, true, false);
                    FadeCellsFrom(i, -y, false, true);
                    FadeCellsFrom(-i, -y, false, false);
                }
            }
            xPrev = xPositive;

        }

        // draw water tiles for the top and bottom edge of the ellipse
        for (int i = xPrev - 1; i >= 0; i--)
        {
            Vector3Int mapPositionTopRight = CellToMap(i, height / 2 - 1);
            _tiles[mapPositionTopRight.x, mapPositionTopRight.y].z = 0f;
            _tiles[mapPositionTopRight.x, mapPositionTopRight.y].zLayer = 0;

            Vector3Int mapPosition = CellToMap(-i, height / 2 - 1);
            _tiles[mapPosition.x, mapPosition.y].z = 0f;
            _tiles[mapPosition.x, mapPosition.y].zLayer = 0;
        }
    }


//     void MakeIsland(bool ellipse)
//     {
//         int xPrev = width / 2; // right edge
//         // // draw water over the right edge of the ellipse
//         for (int i = 0; i < height / 2; i++)
//         {
//             _tmpTilemap.SetTile(new Vector3Int(xPrev, i, 0), ruleTiles[0]);
//         }
//         // _tmpTilemap.SetTile(new Vector3Int(-xPrev, 0, 0), ruleTiles[0]);

//         float a = (float)width / 2f;
//         float b = (float)height / 2f;

//         for (int y = 1; y < height / 2; y++)
//         {
//             float fY = (float)y;
//             float fX = Mathf.Sqrt((1f - (fY * fY) / (b * b)) * (a * a));

//             int x = Mathf.RoundToInt(fX);
//             if (x < xPrev - 1)
//             {
//                 // make up for the missing tiles on the right
//                 for (int i = xPrev - 1; i > x; i--)
//                 {
//                     // draw water over the edge of the ellipse for the missing tiles
//                     for (int j = y; j < height / 2; j++)
//                     {
//                         _tmpTilemap.SetTile(new Vector3Int(i, j, 0), ruleTiles[0]);
//                     }
//                     // draw the missing tile
//                     // _tmpTilemap.SetTile(new Vector3Int(i, y, 0), ruleTiles[0]);
//                     // _tmpTilemap.SetTile(new Vector3Int(i, -y, 0), ruleTiles[0]);
//                     // _tmpTilemap.SetTile(new Vector3Int(-i, y, 0), ruleTiles[0]);
//                     // _tmpTilemap.SetTile(new Vector3Int(-i, -y, 0), ruleTiles[0]);
//                 }
//             }
//             xPrev = x;

// /// CACCA
//             // draw water over the edge of the ellipse for the current tile
//             for (int i = y; i < height / 2; i++)
//             {
//                 _tmpTilemap.SetTile(new Vector3Int(x, i, 0), ruleTiles[0]);
//             }
//             // _tmpTilemap.SetTile(new Vector3Int(x, -y, 0), ruleTiles[0]);
//             // _tmpTilemap.SetTile(new Vector3Int(-x, y, 0), ruleTiles[0]);
//             // _tmpTilemap.SetTile(new Vector3Int(-x, -y, 0), ruleTiles[0]);

//             int zLayer = 0;
//             int curr = x;
//             while (zLayer < _zLayers)
//             {
//                 _tmpTilemap.SetTile(new Vector3Int(curr, y, 0), ruleTiles[zLayer]);
//                 curr--;
//                 zLayer++;
//             }

//         }

//         // draw water tiles for the top edge of the ellipse
//         for (int j = xPrev - 1; j >= 0; j--)
//         {
//             _tmpTilemap.SetTile(new Vector3Int(j, height / 2 - 1, 0), ruleTiles[0]);
//             // _tmpTilemap.SetTile(new Vector3Int(i, -height / 2, 0), ruleTiles[0]);
//             // _tmpTilemap.SetTile(new Vector3Int(-i, height / 2, 0), ruleTiles[0]);
//             // _tmpTilemap.SetTile(new Vector3Int(-i, -height / 2, 0), ruleTiles[0]);
//         }

//     }

    void MakeIsland()
    {
        float zAmount;
        int zLayer;

        // top side
        zAmount = 0f;
        zLayer = 0;
        for (int y = height - 1; y > height - _zLayers - 2; y--)
        {
            for (int x = 0; x < width; x++)
            {
                if (_tiles[x, y].z * _totalWeight > zAmount)
                {
                    _tiles[x, y].z = zAmount / _totalWeight;
                    _tiles[x, y].zLayer = zLayer;
                }
            }
            zAmount += zLayer < weights.Length ? weights[zLayer] : 1f;
            zLayer++;
        }

        // right side
        zAmount = 0f;
        zLayer = 0;
        for (int x = width - 1; x > width - _zLayers - 2; x--)
        {
            for (int y = 0; y < height; y++)
            {
                if (_tiles[x, y].z * _totalWeight > zAmount)
                {
                    _tiles[x, y].z = zAmount / _totalWeight;
                    _tiles[x, y].zLayer = zLayer;
                }
            }
            zAmount += zLayer < weights.Length ? weights[zLayer] : 1f;
            zLayer++;
        }

        // bottom side
        zAmount = 0f;
        zLayer = 0;
        for (int y = 0; y < _zLayers - 1; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (_tiles[x, y].z * _totalWeight > zAmount)
                {
                    _tiles[x, y].z = zAmount / _totalWeight;
                    _tiles[x, y].zLayer = zLayer;
                }
            }
            zAmount += zLayer < weights.Length ? weights[zLayer] : 1f;
            zLayer++;
        }

        // left side
        zAmount = 0f;
        zLayer = 0;
        for (int x = 0; x < _zLayers - 1; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (_tiles[x, y].z * _totalWeight > zAmount)
                {
                    _tiles[x, y].z = zAmount / _totalWeight;
                    _tiles[x, y].zLayer = zLayer;
                }
            }
            zAmount += zLayer < weights.Length ? weights[zLayer] : 1f;
            zLayer++;
        }

    }

    void AddWaterBorder()
    {
        for (int x = -islandWaterBorder; x < width + islandWaterBorder; x++)
        {
            for (int y = -islandWaterBorder; y < 0; y++)
            {
                //Vector3Int tPos = new Vector3Int(centerMap ? x - width / 2 : x, centerMap ? y - height / 2 : y, 0);
                //_tilemaps[0].SetTile(tPos, ruleTiles[0]);
                _tilemaps[0].SetTile(MapToCell(x, y), ruleTiles[0]);

                Vector3Int bPos = new Vector3Int(centerMap ? x - width / 2 : x, (centerMap ? y - height / 2 : y) + islandWaterBorder + height, 0);
                _tilemaps[0].SetTile(bPos, ruleTiles[0]);
            }
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = -islandWaterBorder; x < 0; x++)
            {
                //Vector3Int lPos = new Vector3Int(centerMap ? x - width / 2 : x, centerMap ? y - height / 2 : y, 0);
                //_tilemaps[0].SetTile(lPos, ruleTiles[0]);
                _tilemaps[0].SetTile(MapToCell(x, y), ruleTiles[0]);

                Vector3Int rPos = new Vector3Int((centerMap ? x - width / 2 : x) + islandWaterBorder + width, centerMap ? y - height / 2 : y, 0);
                _tilemaps[0].SetTile(rPos, ruleTiles[0]);
            }
        }
    }

    void OverlapEdges()
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                //Vector3Int pos = new Vector3Int(centerMap ? x - width / 2 : x, centerMap ? y - height / 2 : y, 0);
                Vector3Int pos = MapToCell(x, y);
                int index = _tiles[x, y].zLayer;

                // up
                if ((y < height - 1) && (_tiles[x, y + 1].zLayer == index - 1))
                {
                    _tilemaps[index].SetTile(new Vector3Int(pos.x, pos.y + 1, pos.z), ruleTiles[index]);
                }

                // up-right
                if ((y < height - 1) && (x < width - 1) && (_tiles[x + 1, y + 1].zLayer == index - 1))
                {
                    _tilemaps[index].SetTile(new Vector3Int(pos.x + 1, pos.y + 1, pos.z), ruleTiles[index]);
                }

                // right
                if ((x < width - 1) && (_tiles[x + 1, y].zLayer == index - 1))
                {
                    _tilemaps[index].SetTile(new Vector3Int(pos.x + 1, pos.y, pos.z), ruleTiles[index]);
                }

                // right-down
                if ((y > 0) && (x < width - 1) && (_tiles[x + 1, y - 1].zLayer == index - 1))
                {
                    _tilemaps[index].SetTile(new Vector3Int(pos.x + 1, pos.y - 1, pos.z), ruleTiles[index]);
                }

                // down
                if ((y > 0) && (x < width - 1) && (_tiles[x, y - 1].zLayer == index - 1))
                {
                    _tilemaps[index].SetTile(new Vector3Int(pos.x, pos.y - 1, pos.z), ruleTiles[index]);
                }

                // down-left
                if ((x > 0) && (y > 0) && (_tiles[x - 1, y - 1].zLayer == index - 1))
                {
                    _tilemaps[index].SetTile(new Vector3Int(pos.x - 1, pos.y - 1, pos.z), ruleTiles[index]);
                }

                // left
                if ((x > 0) && (_tiles[x - 1, y].zLayer == index - 1))
                {
                    _tilemaps[index].SetTile(new Vector3Int(pos.x - 1, pos.y, pos.z), ruleTiles[index]);
                }

                // left-up
                if ((y < height - 1) && (x > 0) && (_tiles[x - 1, y + 1].zLayer == index - 1))
                {
                    _tilemaps[index].SetTile(new Vector3Int(pos.x - 1, pos.y + 1, pos.z), ruleTiles[index]);
                }
            }
        }
    }

}
