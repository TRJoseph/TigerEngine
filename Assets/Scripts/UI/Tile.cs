using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Tile : MonoBehaviour
{
    [SerializeField] private Color _lightColor, _darkColor;
    [SerializeField] private SpriteRenderer _spriteRenderer;

    public GameObject OccupyingPiece;

    public struct Distances
    {
        public int DistanceNorth;
        public int DistanceSouth;
        public int DistanceEast;
        public int DistanceWest;

        public int DistanceNorthWest;
        public int DistanceNorthEast;
        public int DistanceSouthWest;
        public int DistanceSouthEast;

    } public Distances distances;


    public void SetTileColor(bool isLightSquare)
    {
        _spriteRenderer.color = isLightSquare ? _lightColor : _darkColor;
    }
}
