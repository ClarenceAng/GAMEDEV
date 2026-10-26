using System.Collections.Generic;
using UnityEngine;

// an event you can make as an asset. the game raises it and anything that registered gets called.
// this way the audio and camera dont need to know about the game script at all
[CreateAssetMenu(menuName = "Snake's Temptation/Game Event", fileName = "GameEvent")]
public class GameEvent : ScriptableObject
{
    List<System.Action> listeners = new List<System.Action>();

    public void Raise()
    {
        for (int i = 0; i < listeners.Count; i++)
        {
            listeners[i]();
        }
    }

    public void Register(System.Action listener)
    {
        listeners.Add(listener);
    }

    // IMPORTANT: always unregister when your object is destroyed, the asset stays around between scenes
    public void Unregister(System.Action listener)
    {
        listeners.Remove(listener);
    }

    public int ListenerCount
    {
        get { return listeners.Count; }
    }
}
