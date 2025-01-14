using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoomInfo : MonoBehaviour
{
    // Contains specific data about rooms, such as exit locations
    // Does not contain properties such as a room's rarity or type, that stays in Room.cs
    // Attached to each RoomObj, contains variables that need to be accessed during gameplay
    public List<Exit> exitLocations;
    public CameraFocus focus;
    public Vector3 roomExtents;
    public Transform roomCenter;
    public Transform rewardSpawn;
    [SerializeField] GameObject stairs;
    [SerializeField] Transform enemies;
    public bool isStaircaseRoom = false;
    public bool isRoomCleared = false;

    public GameObject ceiling;
    public List<Exit> GetExitsByDirection(ExitDirection dir)
    {
        List<Exit> exits = new List<Exit>();
        
        foreach(Exit e in exitLocations)
        {
            if(e.direction == dir)
                exits.Add(e);
        }

        if (exits.Count == 0)
        {
            Debug.Log("No exits found that match direction, returning exit list of size 1 containing exit 0");
            exits.Add(exitLocations[0]);
        }

        return exits;
    }

    // Purely for debugging purposes
    public void ClearExitConnections()
    {
        foreach(Exit e in exitLocations)
        {
            e.connectedRoom = null;
        }
    }

    public void SetExitOpenStatus(bool open)
    {
        foreach(Exit ex in exitLocations)
        {
            if(open && ex.HasConnectingRoom())
                ex.Open();
            else
                ex.Close();
        }
    }

    public void MarkAsStairsRoom()
    {
        isStaircaseRoom = true;
    }

    public void ActivateStairs()
    {
        if(isStaircaseRoom)
        {
            stairs.SetActive(true);
        }
    }

    bool AreEnemiesAlive()
    {
        return enemies.childCount > 0;
    }

    public void FreezeEnemies()
    {
        foreach(EnemyAI e in enemies.GetComponentsInChildren<EnemyAI>())
        {
            e.enabled = false;
        }
    }

    public void UnfreezeEnemies()
    {
        foreach (EnemyAI e in enemies.GetComponentsInChildren<EnemyAI>())
        {
            e.enabled = true;
        }
    }

    void PlayerEnteredRoom(RoomInfo room)
    {
        if (room != this)
        {
            FreezeEnemies();
            return;
        }

        RevealOnMap(room);
        UnfreezeEnemies(); 

        if(AreEnemiesAlive())
        {
            SetExitOpenStatus(false);
        }
    }

    void ClearRoom(RoomInfo r)
    {
        if (r != this)
            return;

        isRoomCleared = true;
        SetExitOpenStatus(true);
        ActivateStairs();
    }

    void CheckForRoomClear()
    {
        if (!isRoomCleared)
        {
            if (!AreEnemiesAlive())
            {
                EventManager.instance.RoomCleared(this);
            }
        }
    }

    public void RevealOnMap(RoomInfo room)
    {
        room.ceiling.gameObject.SetActive(false);
    }

    private void OnDrawGizmos()
    {

    }

    // Start is called before the first frame update
    void Start()
    {
        roomExtents = GetComponent<BoxCollider>().size;
        EventManager.instance.onRoomEntered += PlayerEnteredRoom;
        EventManager.instance.roomCleared += ClearRoom;
        if(!AreEnemiesAlive())
        {
            ClearRoom(this);
        }
        FreezeEnemies();
    }

    private void OnDestroy()
    {
        EventManager.instance.onRoomEntered -= PlayerEnteredRoom;
        EventManager.instance.roomCleared -= ClearRoom;
    }


    private void Update()
    {
        CheckForRoomClear();
    }
}
