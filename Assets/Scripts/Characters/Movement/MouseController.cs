using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;

public class MouseController : MonoBehaviour
{
    public float speed;
    private Touch touch;
    public GameObject characterPrefab;
    public TileBase WaterTile;
    public TileBase IceBreakAnimation;
    public TileBase PackedIceBreakAnimation;
    public TileBase BlackIceBreakAnimation;
    public TileBase IceFormingAnimation;
    public TileBase PackedIceCracked;
    public TileBase BlackIceCracked1;
    public TileBase BlackIceCracked2;
    private CharacterInfo character;
    private Pathfinder pathFinder;
    private RangeFinder rangeFinder;
    private List<OverlayTile> path;
    private List<OverlayTile> inRangeTiles = new List<OverlayTile>();
    public Tilemap tileMap;
    private OverlayTile startTile;
    private OverlayTile endTile;
    private AudioSource currentSoundSource;
    public AudioClip backgroundMusic;
    public bool playBackgroundMusic = false;
    public float musicVolume = 0.8f;
    public MapManager mapManager;
    private bool isMoving = false;
    public int totalBlocksNeeded;
    public UpdateBlocksBroken updateBlocksBroken;
    private bool IceBreaking = true;

    // Start is called before the first frame update
    void Start()
    {
        pathFinder = new Pathfinder();
        rangeFinder = new RangeFinder();
        path = new List<OverlayTile>();

        currentSoundSource = GetComponentInChildren<AudioSource>();

        if(playBackgroundMusic == true)
        {
            currentSoundSource.clip = backgroundMusic;
            currentSoundSource.volume = musicVolume;
            currentSoundSource.loop = true;
            currentSoundSource.Play();
        }

        if(updateBlocksBroken != null)
        {
            updateBlocksBroken.UpdateBlocksBrokenText(0, totalBlocksNeeded);
        }
    }

    void LateUpdate()
    {
        //if character isnt spawned in spawn him in
        if (character == null && mapManager.spawnLocation)
        {
            character = Instantiate(characterPrefab).GetComponent<CharacterInfo>();
            character.standingOnTile = mapManager.spawnLocation;
            PositionCharacterOnLine(mapManager.spawnLocation);
            GetInRangeTiles();
        }

        RaycastHit2D? focusedTileHit = GetFocusedOnTile(); 
        
        if (focusedTileHit.HasValue && isMoving == false)
        {
            if(focusedTileHit.Value.collider.gameObject.GetComponent<OverlayTile>())
            {
                OverlayTile overlayTile = focusedTileHit.Value.collider.gameObject.GetComponent<OverlayTile>();
                transform.position = overlayTile.transform.position;
                gameObject.GetComponentInChildren<SpriteRenderer>().sortingOrder = overlayTile.GetComponent<SpriteRenderer>().sortingOrder;

                overlayTile.ShowTile();

                startTile = character.standingOnTile;
                endTile = overlayTile;
                path = pathFinder.FindPath(character.standingOnTile, overlayTile, inRangeTiles);

                overlayTile.HideTile();
            }
        }

        if(path.Count > 0 && endTile != null)
        {   
            StartCoroutine(MoveAlongPath(startTile, endTile));
            isMoving = true;
        }

        //Show current available path for player character after moving
        if(path.Count == 0)
        {
            isMoving = false;
            endTile = mapManager.spawnLocation;
            GetInRangeTiles();
            isMoving = false;
            StopCoroutine(MoveAlongPath(character.standingOnTile, endTile));
        }
    }

    private void GetInRangeTiles()
    {
        inRangeTiles = rangeFinder.GetTilesInRange(character.standingOnTile, 3);
    }

    private IEnumerator MoveAlongPath(OverlayTile startingTile, OverlayTile end)
    {
        var step = speed * Time.deltaTime;
        var previousTile = path[0];

        float zIndex = path[0].transform.position.z;
        character.transform.position = Vector2.MoveTowards(character.transform.position, path[0].transform.position, step);
        // Take z index to make sure character has a "height" in the 2D space
        character.transform.position = new Vector3(character.transform.position.x , character.transform.position.y, zIndex);

        if(Vector2.Distance(character.transform.position, path[0].transform.position) < 0.0001f)
        {   
            if(startingTile.ice == true && startingTile != previousTile && IceBreaking)
            {   
                IceTileChecker(startingTile);
                startTile = mapManager.overlayTilePrefab;
            }

            PositionCharacterOnLine(path[0]);

            if(previousTile.ice == true && previousTile != end && previousTile != startingTile && IceBreaking)
            {   
                IceTileChecker(previousTile);
            }

            path.RemoveAt(0);
        }
        yield return null;
    }

    private void IceTileUpdater(OverlayTile tile)
    {
        tile.hp -= 1;
        if(tile.hp == 1 && tileMap.GetTile(tile.gridLocation) == mapManager.packedIceTile)
        {
            TileCrack(tile, PackedIceCracked);
            character.PlayIceAlmostBreakingSound();
        } else if (tile.hp == 1 && tileMap.GetTile(tile.gridLocation) == BlackIceCracked1)
        {
            TileCrack(tile, BlackIceCracked2);
            character.PlayIceAlmostBreakingSound();
        } else if(tile.hp == 2)
        {
            TileCrack(tile, BlackIceCracked1);
            character.PlayIceCrackingSound();
        }
    }
    private void IceTileChecker(OverlayTile tile)
    {
        // Change Iceblock and refresh the tilemap 
        if(tile.hp == 2 && tileMap.GetTile(tile.gridLocation) == mapManager.packedIceTile)
        {
            IceTileUpdater(tile);
        } else if(tile.hp == 2 && tileMap.GetTile(tile.gridLocation) == BlackIceCracked1)
        {
            IceTileUpdater(tile);
        } else if(tile.hp == 3)
        {
            IceTileUpdater(tile);
        } else if(tile.hp == 1 && tileMap.GetTile(tile.gridLocation) == mapManager.iceTile)
        {
            IceTileUpdater(tile);
            TileAnimation(tile, IceBreakAnimation);
        } else if(tile.hp == 1 && tileMap.GetTile(tile.gridLocation) == IceFormingAnimation)
        {
            IceTileUpdater(tile);
            TileAnimation(tile, IceBreakAnimation);
        } else if(tile.hp == 1 && tileMap.GetTile(tile.gridLocation) == PackedIceCracked)
        {
            IceTileUpdater(tile);
            TileAnimation(tile, PackedIceBreakAnimation);
        } else if(tile.hp == 1 && tileMap.GetTile(tile.gridLocation) == BlackIceCracked2)
        {
            IceTileUpdater(tile);
            TileAnimation(tile, BlackIceBreakAnimation);
        }
    }

    private void TileCrack(OverlayTile tile, TileBase tileBase)
    {
        tileMap.SetTile(tile.gridLocation, tileBase);
        tileMap.RefreshTile(tile.gridLocation);
    }
    private void TileAnimation(OverlayTile tile, TileBase tileBase)
    {
        tile.isBlocked = true;
        tileMap.SetTile(tile.gridLocation, tileBase);
        character.PlayIceBreakingSound();
        tileMap.RefreshTile(tile.gridLocation);

        character.brokenIceBlocks += 1;
        if(updateBlocksBroken != null)
        {
            updateBlocksBroken.UpdateBlocksBrokenText(character.brokenIceBlocks, totalBlocksNeeded);
        }
        
        if(character.brokenIceBlocks == totalBlocksNeeded)
        {
            OpenPath();
        }

        StartCoroutine(PlayTileAnimationAfterDelay(tile));
    }

    private IEnumerator PlayTileAnimationAfterDelay(OverlayTile tile)
    {
        yield return new WaitForSeconds(.8f);

        // Change Iceblock to Watertile at the end of the animation
        tileMap.SetTile(tile.gridLocation, WaterTile);
        tileMap.RefreshTile(tile.gridLocation);
        yield break;
    }

    public void OpenPath()
    {
        tileMap.SetTile(mapManager.bridgeTile.gridLocation, IceFormingAnimation);
        tileMap.RefreshTile(mapManager.bridgeTile.gridLocation);
        mapManager.bridgeTile.isBlocked = false;
    }

    public void ToggleIceBreaking()
    {
        if(IceBreaking)
        {
            IceBreaking = false;
        } else 
        {
            IceBreaking = true;
        }
    }

    public void RefreshMap()
    {
        if (tileMap != null)
        {
            tileMap.RefreshAllTiles();
        }
    }

    public RaycastHit2D? GetFocusedOnTile()
    {
        if(Input.touchCount > 0)
        {
            touch = Input.GetTouch(0);

            // Handle finger movements based on TouchPhase
            switch (touch.phase)
            {
                //When a touch has first been detected, change the message and record the starting position
                case (UnityEngine.TouchPhase)UnityEngine.InputSystem.TouchPhase.Began:
                    break;
                case (UnityEngine.TouchPhase)UnityEngine.InputSystem.TouchPhase.Ended:
                    // Record initial touch position.
                    Vector3 touchPos = Camera.main.ScreenToWorldPoint(touch.position);
                    Vector2 touchPos2d = new Vector2(touchPos.x, touchPos.y);

                    RaycastHit2D[] hits = Physics2D.RaycastAll(touchPos2d, Vector2.zero);

                    if (hits.Length > 0)
                    {
                        return hits.OrderByDescending(i => i.collider.transform.position.z).First();
                    }
                    break;
            }
        }

        return null;
    }

    private void PositionCharacterOnLine(OverlayTile tile)
    {
        // Place character on clicked tile
        character.transform.position = new Vector3(tile.transform.position.x, tile.transform.position.y, tile.transform.position.z);
        character.GetComponentInChildren<SpriteRenderer>().sortingOrder = tile.GetComponent<SpriteRenderer>().sortingOrder;
        character.standingOnTile = tile;
    }
}
