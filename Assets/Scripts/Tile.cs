using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Tile : MonoBehaviour
{
    [SerializeField] private Color _lightColor, _darkColor;
    [SerializeField] private SpriteRenderer _spriteRenderer;

    public GameObject OccupyingPiece;

    public void SetTileColor(bool isLightSquare)
    {
        _spriteRenderer.color = isLightSquare ? _lightColor : _darkColor;
    }
}
