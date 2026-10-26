using System.Collections.Generic;
using UnityEngine;

// goes on the snake head (trigger sphere + kinematic rigidbody).
// it remembers everything it bumped into until the list gets cleared
public class HeadSensor : MonoBehaviour
{
    public List<Contact> Touched = new List<Contact>();

    void OnTriggerEnter(Collider other)
    {
        Touchable touchable = other.GetComponent<Touchable>();
        if (touchable != null)
        {
            Touched.Add(touchable.kind);
        }
    }
}
