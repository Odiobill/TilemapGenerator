# (Topdown) Tilemap Generator for Unity

Generates one or more traditional or hexagonal tilemaps based on provided Rule Tiles and a source (empty) Tilemap that will be used as a reference.
Each Rule Tile will represent a "layer", calculated using Perlin Noise, and will be placed on its own Tilemap.
The "Sorting Order" of any generated Tilemap (Renderer) will be incremented depending on the calculated layer.
You are also required to provide a "Weight" for each Rule Tile/layer. The weight defines "how big" a layer should be compared to the others.
The generated tilemaps will be created as children of the empty template ones, to avoid messing up with other tilemaps that you may want to use outside the scope of this script.

## Parameters available from the Inspector:
- *Rule Tiles8            add a Rule Tile for each layer that you want to create
- *Weights*               assign a weight to each layer to define how big it should be compared to the other ones.
- *Width*                 the width of the Map
- *Height*                the height of the Map
- *Scale*                 affects the Perlin Noise calculation; you may want to adjust this depending on the Width and Height of your Map
- *Randomize*             use a random Seed; ignored if calling Generate(true) from script, but not with Generate() or Generate(false)
- *Seed*                  base for the Perlin Noise calculation; the same Seed (with the same Scale) will always generate the same Map
- *Center Map*            Cell(0, 0) will be in the center of the generated Map
- *Fade Out Tiles*        check this option if your rule tiles only provide rules to fade out to the next layer
- *Island Water Border*   if greater than zero, fades the edges of the Map to the first layer and then adds a border of the specified size around the Map

## Usage
- Download and install the required Unity 2d-extras package from "https://github.com/Unity-Technologies/2d-extras"
   As specified in the README.md file of that repository, you can just add
   _"com.unity.2d.tilemap.extras": "https://github.com/Unity-Technologies/2d-extras.git#master"_
   to your _Packages/manifest.json_ file in your Unity Project under the dependencies section
- Create an empty Tilemap, either for traditional or hexagonal tiles, that will be used as a template for the generated ones
- Attach this script to the Grid GameObject containing the template Tilemap
- In the Inspector, assign a Rule Tile for any layer you want in the "Rule Tiles" list
- Define a weight for each layer in the "Weights" list
- Fill up the additional parameters as explained above
- Press "Generate Tilemaps" in the Editor to create the tilemaps, or call the Generate() method (provided by the API) from another script

## Additional API methods and references
- *grid*                  The grid component to which this script is attached
- *Map*                   TileData matrix representing the (calculated) tiles
- *TilemapsGenerated*     True if any generated Tilemap already exists
- *CellToMap()*           Converts a position from Cell to Map
- *MapToCell()*           Converts a position from Map to Cell
- *GetTileDataMap()*      Returns a TileData object from the specified position in the Map
- *GetTileDataCell()*     Returns a TileData object from the specified position in the grid
- *GetTileDataWorld()*    Returns a TileData object from the specified World position
- *Generate()*            Generates (or just calculates, if passing "true" as parameter) new Tilemaps

If you generated tilemaps from the Inspector and you want to use the Tilemap Generator API from your scripts during run-time, remember to always call _Generate(true)_ first, before any other provided function, using the same parameters in the Inspector that generated those tilemaps.
