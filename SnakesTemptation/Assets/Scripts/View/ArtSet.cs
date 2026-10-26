using UnityEngine;

// all the materials the game uses, set in the inspector on the game object
[System.Serializable]
public class ArtSet
{
    [Header("Retro look (game boy colors)")]
    public Material retroBase;
    public Material retroTileA;
    public Material retroTileB;
    public Material retroSnake;
    public Material retroBorder;
    public Material retroSprite; // for the pixel apple

    [Header("3D look")]
    public Material ground;
    public Material tileA;
    public Material tileB;
    public Material plazaA;
    public Material plazaB;
    public Material hedge;
    public Material trunk;
    public Material foliage;
    public Material snakeBody;
    public Material snakeHead;
    public Material eyeWhite;
    public Material pupil;
    public Material seed;
    public Material seedBeam;
    public Material lantern;
    public Material lanternBeam;

    [Header("Enemy")]
    public GameObject monsterPrefab;
}
