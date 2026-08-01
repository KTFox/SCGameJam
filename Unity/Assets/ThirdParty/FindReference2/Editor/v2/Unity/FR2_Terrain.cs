using System;
using UnityEngine;

namespace vietlabs.fr2
{

    internal static class FR2_Terrain
    {
	    [Serializable] internal class TerrainTextureData
	    {
		    public Texture2D[] textures;
		    public TerrainTextureData(params Texture2D[] param)
		    {
			    var count = 0;
			    if (param != null) count = param.Length;

			    textures = new Texture2D[count];
			    for (var i = 0; i < count; i++)
			    {
				    textures[i] = param[i];
			    }
		    }
	    }
	    
	    internal static int ReplaceTerrainTextureDatas(TerrainData terrain, Texture2D fromObj, Texture2D toObj)
	    {
		    var found = 0;
		    TerrainLayer[] arr3 = terrain.terrainLayers;
		    for (var i = 0; i < arr3.Length; i++)
		    {
			    if (arr3[i].normalMapTexture == fromObj)
			    {
				    found++;
				    arr3[i].normalMapTexture = toObj;
			    }

			    if (arr3[i].maskMapTexture == fromObj)
			    {
				    found++;
				    arr3[i].maskMapTexture = toObj;
			    }

			    if (arr3[i].diffuseTexture == fromObj)
			    {
				    found++;
				    arr3[i].diffuseTexture = toObj;
			    }
		    }

		    terrain.terrainLayers = arr3;
		    return found;
	    }
	    
        internal static TerrainTextureData[] GetTerrainTextureDatas(TerrainData data)
        {
            if (data == null || data.terrainLayers == null)
            {
                return Array.Empty<TerrainTextureData>();
            }
            
            var arr = new TerrainTextureData[data.terrainLayers.Length];
            for (var i = 0; i < data.terrainLayers.Length; i++)
            {
                TerrainLayer layer = data.terrainLayers[i];
                arr[i] = layer == null ? new TerrainTextureData()
                    : new TerrainTextureData(
                        layer.normalMapTexture,
                        layer.maskMapTexture,
                        layer.diffuseTexture
                    );
            }

            return arr;
        }
    }
}