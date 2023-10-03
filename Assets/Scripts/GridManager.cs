using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    // reference to game tile prefab
    [SerializeField] private Tile tilePrefab;

    [SerializeField] private Transform _cam;

    // Start is called before the first frame update
    void Start()
    {
        GenerateGrid();
    }

    void GenerateGrid()
    {
        int file = 0;
        int rank = 0;
        for (file = 0; file < 8; file++)
        {
            for (rank = 0; rank < 8; rank++)
            {
                var tile = Instantiate(tilePrefab, new Vector3(file, rank, 0), Quaternion.identity);

                bool isLightSquare = (file + rank) % 2 != 0;

                tile.SetTileColor(isLightSquare);

                tile.name = $"Tile {file} {rank}";
            }
        }

        _cam.transform.position = new Vector3((float)file / 2 - 0.5f, (float)rank / 2 - 0.5f, -10);

    }
}
